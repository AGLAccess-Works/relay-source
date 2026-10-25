using ControllerMouse.Core;

internal static class SetupPlanTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool value) { if (!value) throw new Exception("Setup plan assertion failed."); }
        static bool Offers(Settings settings, Shortcut action) => ControlMapping.Controls.Any(c => ControlMapping.Resolve(settings, c.Control).Shortcut == action) ||
            settings.ButtonChords?.Any(c => c.Action.Shortcut == action) == true;
        static bool ReachableTool(Settings settings, Shortcut action) => Offers(settings, action) ||
            Offers(settings, Shortcut.AccessibilityHub) && AccessibilityCatalog.Actions.Any(option => option.Action == action);
        static void FixedSwitchNavigation(Settings settings)
        {
            Check(ControlMapping.Resolve(settings, Control.A) == new ControlBinding(BindingKind.Keyboard, Key: 13));
            Check(ControlMapping.Resolve(settings, Control.B) == new ControlBinding(BindingKind.Keyboard, Key: 9));
            Check(ControlMapping.Resolve(settings, Control.X) == new ControlBinding(BindingKind.Keyboard, Key: 9, Modifiers: 4));
            Check(ControlMapping.Resolve(settings, Control.Y) == new ControlBinding(BindingKind.Keyboard, Key: 27));
            Check(ControlMapping.Resolve(settings, Control.LeftShoulder) == new ControlBinding(BindingKind.Keyboard, Key: 9, Modifiers: 4));
            Check(ControlMapping.Resolve(settings, Control.RightShoulder) == new ControlBinding(BindingKind.Keyboard, Key: 9));
            foreach (var (control, action) in new[] { (Control.Up, Shortcut.Up), (Control.Down, Shortcut.Down),
                (Control.Left, Shortcut.Left), (Control.Right, Shortcut.Right) })
                Check(ControlMapping.Resolve(settings, control) == new ControlBinding(BindingKind.Shortcut, Shortcut: action));
        }
        test("setup automatically assigns keyboard and Magnifier combinations", () =>
        {
            var settings = SetupPlan.Build(new() { Magnifier = true }, new());
            Check(settings.ButtonChords!.Any(c => c.First == Control.A && c.Second == Control.B && c.Action.Shortcut == Shortcut.OnScreenKeyboard));
            Check(settings.ButtonChords!.Any(c => c.First == Control.LeftShoulder && c.Second == Control.RightShoulder && c.Action.Shortcut == Shortcut.MagnifierView));
            Check(ControlMapping.Resolve(settings, Control.A).Kind == BindingKind.Mouse && ControlMapping.Resolve(settings, Control.B).Kind == BindingKind.Mouse);
        });
        test("every setup choice combination yields valid mappings and all selected tools", () =>
        {
            for (int mask = 0; mask < 1024; mask++)
            {
                var choices = new SetupChoices
                {
                    LargerText = (mask & 1) != 0,
                    Magnifier = (mask & 2) != 0,
                    ReadAloud = (mask & 4) != 0,
                    SteadyMovement = (mask & 8) != 0,
                    OneButton = (mask & 16) != 0,
                    Keyboard = (mask & 32) != 0,
                    Captions = (mask & 64) != 0,
                    VoiceTyping = (mask & 128) != 0,
                    Quiet = (mask & 256) != 0,
                    LearnMovement = (mask & 512) != 0
                };
                var settings = SetupPlan.Build(choices, new());
                Check(ButtonChord.ValidList(settings.ButtonChords));
                Check(settings.ButtonMappings!.Values.All(b => b.IsValid));
                Check(!choices.OneButton || settings.ButtonChords!.Length == 0);
                Check(!choices.Keyboard || Offers(settings, Shortcut.OnScreenKeyboard));
                Check(!choices.Magnifier || Offers(settings, Shortcut.ZoomIn) && Offers(settings, Shortcut.ZoomOut) && Offers(settings, Shortcut.MagnifierView) && Offers(settings, Shortcut.CloseMagnifier));
                Check(!choices.ReadAloud || Offers(settings, Shortcut.Narrator));
                Check(!choices.Captions || Offers(settings, Shortcut.LiveCaptions));
                Check(!choices.VoiceTyping || Offers(settings, Shortcut.VoiceTyping));
                Check(settings.Haptics != choices.Quiet && settings.TremorStabilization == choices.SteadyMovement);
                Check(settings.MotionLearning == (choices.LearnMovement ? LearningMode.Learn : LearningMode.Off));
                Check(settings.ClickHoldMilliseconds == 0);
            }
        });
        test("setup navigation allows controller clicking without firing accessibility tools", () =>
        {
            var settings = SetupPlan.Navigation(new());
            Check(ControlMapping.Resolve(settings, Control.A).Kind == BindingKind.Mouse);
            Check(settings.ButtonChords == null && settings.MotionLearning == LearningMode.Off && !settings.Haptics);
            Check(!Offers(settings, Shortcut.ZoomIn) && !Offers(settings, Shortcut.Narrator));
        });
        test("setup completion persists independently of active profile settings", () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "relay-setup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                var store = new ProfileStore(Path.Combine(folder, "profiles.json")); var original = store.Load(new());
                Check(!original.SetupCompleted);
                store.Save(original with { SetupCompleted = true });
                Check(store.Load(new()).SetupCompleted);
                var planned = SetupPlan.Build(new() { LargerText = true }, new() { LightTheme = true, MinimizeToTray = false });
                Check(planned.InterfaceScale == 1.5 && planned.LightTheme && !planned.MinimizeToTray);
            }
            finally { Directory.Delete(folder, true); }
        });
        test("setup preserves controller preference and recorded accessories", () =>
        {
            var preferences = new Settings
            {
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [new("A", "My switch", AccessoryKind.Switch, [AccessorySignal.A])]
            };
            foreach (var settings in new[] { SetupPlan.Build(new(), preferences), SetupPlan.Navigation(preferences) })
                Check(settings.ControllerType == ControllerPreference.Adaptive && settings.AdaptiveAccessories?.Single().Name == "My switch");
        });
        test("setup defaults to the existing controller plan", () =>
        {
            Check(new SetupChoices().InputMethod == SetupInputMethod.Controller && new SetupChoices().SimpleControls == null);
            var choices = new SetupChoices { Magnifier = true, ReadAloud = true, LearnMovement = true, VoiceTyping = true };
            Check(System.Text.Json.JsonSerializer.Serialize(SetupPlan.Build(choices, new())) ==
                System.Text.Json.JsonSerializer.Serialize(SetupPlan.Build(choices with { InputMethod = SetupInputMethod.Controller }, new())));
            Check(System.Text.Json.JsonSerializer.Serialize(SetupPlan.Navigation(new())) ==
                System.Text.Json.JsonSerializer.Serialize(SetupPlan.Navigation(new(), SetupInputMethod.Controller)));
        });
        test("every switch and pedal setup combination keeps fixed single-input navigation and reachable tools", () =>
        {
            foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
                for (int mask = 0; mask < 1024; mask++)
                {
                    var choices = new SetupChoices
                    {
                        InputMethod = method,
                        LargerText = (mask & 1) != 0,
                        Magnifier = (mask & 2) != 0,
                        ReadAloud = (mask & 4) != 0,
                        SteadyMovement = (mask & 8) != 0,
                        OneButton = (mask & 16) != 0,
                        Keyboard = (mask & 32) != 0,
                        Captions = (mask & 64) != 0,
                        VoiceTyping = (mask & 128) != 0,
                        Quiet = (mask & 256) != 0,
                        LearnMovement = (mask & 512) != 0
                    };
                    var settings = SetupPlan.Build(choices, new());
                    FixedSwitchNavigation(settings);
                    Check(settings.ButtonChords?.Length == 0 && settings.MotionLearning == LearningMode.Off && !settings.TargetAssist);
                    Check(!settings.Haptics && !settings.MagnifierHaptics && !settings.ScrollHaptics && !settings.TargetHaptics && !settings.BoundaryHaptics);
                    Check(settings.ButtonMappings!.Values.All(binding => binding.IsValid && binding.Kind is not (BindingKind.Mouse or BindingKind.Pointer or BindingKind.Precision or BindingKind.Boost)));
                    Check(ControlMapping.Resolve(settings, Control.LeftStick).Kind == BindingKind.None &&
                        ControlMapping.Resolve(settings, Control.RightStick).Kind == BindingKind.None);
                    bool zoom = method == SetupInputMethod.Pedals || choices.Magnifier;
                    Check(ControlMapping.Resolve(settings, Control.LeftTrigger) == (zoom ? new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.ZoomOut) : new ControlBinding()));
                    Check(ControlMapping.Resolve(settings, Control.RightTrigger) == (zoom ? new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.ZoomIn) : new ControlBinding()));
                    Check(Offers(settings, Shortcut.AccessibilityHub));
                    Check(!choices.Keyboard || ReachableTool(settings, Shortcut.OnScreenKeyboard));
                    Check(!choices.Magnifier || ReachableTool(settings, Shortcut.MagnifierView) && ReachableTool(settings, Shortcut.CloseMagnifier));
                    Check(!choices.ReadAloud || ReachableTool(settings, Shortcut.Narrator));
                    Check(!choices.Captions || ReachableTool(settings, Shortcut.LiveCaptions));
                    Check(!choices.VoiceTyping || ReachableTool(settings, Shortcut.VoiceTyping));
                    Check(settings.TremorStabilization == choices.SteadyMovement && settings.ScrollStabilization == choices.SteadyMovement);
                    Check(settings.ScrollMode == ScrollMode.Step && settings.ScrollPrecision && settings.ClickHoldMilliseconds == 0);
                }
        });
        test("pedal setup always assigns both zoom pedals independently of optional tools", () =>
        {
            foreach (bool magnifier in new[] { false, true })
            {
                var settings = SetupPlan.Build(new()
                {
                    InputMethod = SetupInputMethod.Pedals,
                    Magnifier = magnifier,
                    Keyboard = false,
                    LearnMovement = true
                }, new());
                Check(ControlMapping.Resolve(settings, Control.LeftTrigger).Shortcut == Shortcut.ZoomOut &&
                    ControlMapping.Resolve(settings, Control.RightTrigger).Shortcut == Shortcut.ZoomIn);
                Check(settings.MotionLearning == LearningMode.Off); FixedSwitchNavigation(settings);
            }
        });
        test("adaptive setup spare Menu tool has a predictable priority without requiring stick clicks", () =>
        {
            foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
                foreach (var (choices, action) in new[] {
                    (new SetupChoices { ReadAloud = true, VoiceTyping = true, Captions = true }, Shortcut.Narrator),
                    (new SetupChoices { VoiceTyping = true, Captions = true }, Shortcut.OnScreenKeyboard),
                    (new SetupChoices { Keyboard = false, VoiceTyping = true, Captions = true }, Shortcut.VoiceTyping),
                    (new SetupChoices { Keyboard = false, Captions = true }, Shortcut.LiveCaptions),
                    (new SetupChoices { Keyboard = false, Magnifier = true }, Shortcut.MagnifierStart),
                    (new SetupChoices { Keyboard = false }, Shortcut.StartMenu) })
                {
                    var settings = SetupPlan.Build(choices with { InputMethod = method }, new());
                    Check(ControlMapping.Resolve(settings, Control.Start).Shortcut == action);
                    Check(ControlMapping.Resolve(settings, Control.Back).Shortcut == Shortcut.AccessibilityHub);
                    FixedSwitchNavigation(settings);
                }
        });
        test("switch and pedal setup navigation cannot start any accessibility tool", () =>
        {
            foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
            {
                var settings = SetupPlan.Navigation(new() { MotionLearning = LearningMode.Learn, TargetAssist = true }, method);
                FixedSwitchNavigation(settings);
                Check(settings.ButtonChords?.Length == 0 && settings.MotionLearning == LearningMode.Off && !settings.TargetAssist && !settings.Haptics);
                foreach (var option in ControlMapping.Controls)
                {
                    var binding = ControlMapping.Resolve(settings, option.Control);
                    Check(binding.Kind is BindingKind.None or BindingKind.Keyboard ||
                        binding.Kind == BindingKind.Shortcut && binding.Shortcut is Shortcut.Up or Shortcut.Down or Shortcut.Left or Shortcut.Right);
                }
                Check(ControlMapping.Resolve(settings, Control.LeftTrigger).Kind == BindingKind.None &&
                    ControlMapping.Resolve(settings, Control.RightTrigger).Kind == BindingKind.None);
            }
        });
        test("combined reading and motor setup preserves independent display preferences and actual accessories", () =>
        {
            var visual = new VisualAccessPreferences
            {
                Scheme = DisplayScheme.Custom,
                Spacing = VisualSpacing.Wide,
                Complexity = VisualComplexity.Reduced,
                Motion = VisualMotion.Off,
                Focus = VisualFocus.VeryStrong,
                Highlight = VisualHighlight.Custom,
                CustomHighlight = "#25C9FF",
                OneControlAtATime = true,
                BackgroundColour = "#211337",
                SurfaceColour = "#2C1D45",
                TextColour = "#FFFFFF",
                SecondaryTextColour = "#D8CCEB"
            };
            var preferences = new Settings
            {
                VisualAccess = visual,
                InterfaceScale = 1.8,
                LightTheme = false,
                ReduceInterfaceMotion = true,
                ReadHoveredText = false,
                InputAnnouncements = true,
                ShowControllerLabels = true,
                MinimizeToTray = false,
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [new("A", "My head switch", AccessoryKind.Switch, [AccessorySignal.A])]
            };
            foreach (var method in Enum.GetValues<SetupInputMethod>())
                foreach (var settings in new[] {
                    SetupPlan.Build(new() { InputMethod = method, LargerText = true, ReadAloud = true, Magnifier = true,
                        Captions = true, VoiceTyping = true, SteadyMovement = true, LearnMovement = true }, preferences),
                    SetupPlan.Navigation(preferences, method) })
                {
                    Check(settings.EffectiveVisualAccess == visual && settings.InterfaceScale == 1.8 && !settings.LightTheme);
                    Check(settings.ReduceInterfaceMotion && !settings.ReadHoveredText && settings.InputAnnouncements &&
                        settings.ShowControllerLabels && !settings.MinimizeToTray);
                    Check(settings.ControllerType == preferences.ControllerType && settings.AdaptiveAccessories?.Single().Name == "My head switch");
                }
        });
        test("setup simple controls explicitly changes only the one-control preference", () =>
        {
            foreach (var method in Enum.GetValues<SetupInputMethod>())
                foreach (bool original in new[] { false, true })
                    foreach (bool? simple in new bool?[] { null, false, true })
                    {
                        var visual = new VisualAccessPreferences
                        {
                            Scheme = DisplayScheme.DeepDark,
                            Spacing = VisualSpacing.Wide,
                            Complexity = VisualComplexity.Reduced,
                            Motion = VisualMotion.Off,
                            Focus = VisualFocus.VeryStrong,
                            Highlight = VisualHighlight.Blue,
                            OneControlAtATime = original
                        };
                        var settings = SetupPlan.Build(new() { InputMethod = method, SimpleControls = simple },
                            new() { VisualAccess = visual, InterfaceScale = 1.8, LightTheme = false, ReduceInterfaceMotion = true });
                        Check(settings.EffectiveVisualAccess == (visual with { OneControlAtATime = simple ?? original }));
                        Check(settings.InterfaceScale == 1.8 && !settings.LightTheme && settings.ReduceInterfaceMotion);
                    }
        });
        test("setup never changes the shared adaptive preset mappings", () =>
        {
            string original = System.Text.Json.JsonSerializer.Serialize(AdaptiveAccessibilityProfiles.All);
            foreach (var method in new[] { SetupInputMethod.Switches, SetupInputMethod.Pedals })
            {
                SetupPlan.Build(new() { InputMethod = method, ReadAloud = true, Magnifier = true, Keyboard = true }, new());
                SetupPlan.Navigation(new(), method);
            }
            Check(System.Text.Json.JsonSerializer.Serialize(AdaptiveAccessibilityProfiles.All) == original);
        });
    }
}

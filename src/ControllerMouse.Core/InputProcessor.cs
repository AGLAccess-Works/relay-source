namespace ControllerMouse.Core;

[Flags]
public enum Buttons : ushort
{
    None = 0, Up = 1, Down = 2, Left = 4, Right = 8, Start = 16, Back = 32,
    LeftStick = 64, RightStick = 128, LeftShoulder = 256, RightShoulder = 512,
    A = 4096, B = 8192, X = 16384, Y = 32768
}

public readonly record struct Gamepad(Buttons Buttons, byte LeftTrigger, byte RightTrigger,
    short LeftX, short LeftY, short RightX, short RightY);
public enum MouseButton { Left, Right, Middle }
public enum RelayNavigationCommand { Up, Down, Left, Right, Next, Previous, Select, Back }
public enum Shortcut
{
    StartMenu, Accessibility, ZoomIn, ZoomOut, CloseMagnifier, Up, Down, Left, Right,
    None, AccessibilityHub, Narrator, OnScreenKeyboard, LiveCaptions, ColorFilters, VoiceTyping, MagnifierRead, MagnifierView,
    WindowsOnScreenKeyboard, MagnifierFullScreen, MagnifierLens, MagnifierDocked, MagnifierInvert, ScreenSnipping,
    MagnifierStart, NarratorStart, NarratorStop,
    KeyboardUp, KeyboardDown, KeyboardLeft, KeyboardRight, KeyboardSelect, KeyboardSpace, KeyboardSymbols, KeyboardShift, KeyboardBackspace,
    KeyboardNext, KeyboardPrevious,
    SettingsTextSize = 100, SettingsVisualEffects, SettingsPointer, SettingsCursor, SettingsMagnifier, SettingsColourFilters,
    SettingsScreenTint, SettingsContrast, SettingsNarrator, SettingsAudio, SettingsCaptions, SettingsHearing,
    SettingsSpeech, SettingsKeyboard, SettingsMouse, SettingsEyeControl, SettingsTyping
}

public interface IInputSink
{
    void Move(int x, int y);
    void Scroll(int vertical, int horizontal);
    void SetMouseButton(MouseButton button, bool down);
    void Invoke(Shortcut shortcut);
    void InvokeKeys(ushort[] keys);
}

public interface IRelayInputCleanup { void ReleaseAll(); }

/// <summary>Deterministic input translation, independent of Windows and the polling rate.</summary>
public sealed class InputProcessor(IInputSink sink)
{
    private Settings _settings = new();
    public bool NarratorRunning { get; set; }
    public bool RelayWindowActive { get; set; }
    public Action<RelayNavigationCommand>? RelayNavigationRequested { get; set; }
    public bool ConfigurationMode { get; set; }
    // Set only for a registered, active Relay configuration window. A same-process
    // native dialog or the typing keyboard must not acquire paused navigation.
    public bool PausedNavigationWindowActive { get; set; }
    public bool CanNavigateWhilePaused => !Enabled && RelayWindowActive && PausedNavigationWindowActive &&
        RelayNavigationRequested != null;
    private bool _pausedNavigationActive;
    public bool KeyboardOpen { get; private set; }
    public bool KeyboardSelectionActive { get; private set; }
    public void SetKeyboardOpen(bool open) { Reset(); KeyboardOpen = open; KeyboardSelectionActive = open; }
    public bool KeyboardNavigationActive { get; private set; }
    // Identify the actual four-switch assignments, including customised profiles.
    // A device name cannot establish which switches a person can operate.
    public bool FixedSwitchKeyboardLayout => HasKey(Control.A, 13) && HasKey(Control.B, 9) &&
        HasKey(Control.X, 9, 4) && HasKey(Control.Y, 27);
    private bool HasKey(Control control, int key, int modifiers = 0) =>
        ControlMapping.Resolve(_settings, control) is { Kind: BindingKind.Keyboard } binding &&
        binding.Key == key && binding.Modifiers == modifiers;
    private Buttons _navigationSuppressed;
    private short _navigationStickOriginX, _navigationStickOriginY;
    private static readonly Buttons NavigationButtons = Buttons.Up | Buttons.Down | Buttons.Left | Buttons.Right |
        Buttons.A | Buttons.B | Buttons.LeftShoulder | Buttons.RightShoulder;
    public ControlBinding ResolveBinding(Control control)
    {
        if (ConfigurationMode || CanNavigateWhilePaused) return ResolveConfigurationBinding(control);
        if (KeyboardOpen && KeyboardSelectionActive) return control switch
        {
            Control.Up => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardUp),
            Control.Down => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardDown),
            Control.Left => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardLeft),
            Control.Right => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardRight),
            Control.A => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardSelect),
            Control.B => new(BindingKind.Shortcut, Shortcut: FixedSwitchKeyboardLayout ? Shortcut.KeyboardNext : Shortcut.OnScreenKeyboard),
            Control.X => new(BindingKind.Shortcut, Shortcut: FixedSwitchKeyboardLayout ? Shortcut.KeyboardPrevious : Shortcut.KeyboardBackspace),
            Control.Y => new(BindingKind.Shortcut, Shortcut: FixedSwitchKeyboardLayout ? Shortcut.OnScreenKeyboard : Shortcut.KeyboardSpace),
            Control.LeftTrigger => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardSymbols),
            Control.RightTrigger => new(BindingKind.Shortcut, Shortcut: Shortcut.KeyboardShift),
            Control.LeftShoulder => new(BindingKind.Keyboard, Key: 37) { Repeat = true },
            Control.RightShoulder => new(BindingKind.Keyboard, Key: 39) { Repeat = true },
            _ => ControlMapping.Resolve(_settings, control)
        };
        if (!KeyboardNavigationActive) return ControlMapping.Resolve(_settings, control);
        return control switch
        {
            Control.Up => new(BindingKind.Shortcut, Shortcut: Shortcut.Up),
            Control.Down => new(BindingKind.Shortcut, Shortcut: Shortcut.Down),
            Control.Left => new(BindingKind.Shortcut, Shortcut: Shortcut.Left),
            Control.Right => new(BindingKind.Shortcut, Shortcut: Shortcut.Right),
            Control.A => new(BindingKind.Keyboard, Key: 13),
            Control.B => new(BindingKind.Keyboard, Key: 27),
            Control.LeftShoulder => new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
            Control.RightShoulder => new(BindingKind.Keyboard, Key: 9),
            _ => ControlMapping.Resolve(_settings, control)
        };
    }
    private ControlBinding ResolveConfigurationBinding(Control control)
    {
        var saved = ControlMapping.Resolve(_settings, control);
        // Fixed switch profiles remain usable in the editor without sending desktop actions.
        if (saved.Kind == BindingKind.Keyboard && saved.Key is 9 or 13 or 27 or 37 or 38 or 39 or 40 &&
            (saved.Modifiers == 0 || saved.Key == 9 && saved.Modifiers == 4)) return saved;
        return control switch
        {
            Control.Up => new(BindingKind.Keyboard, Key: 38) { Repeat = true },
            Control.Down => new(BindingKind.Keyboard, Key: 40) { Repeat = true },
            Control.Left => new(BindingKind.Keyboard, Key: 37) { Repeat = true },
            Control.Right => new(BindingKind.Keyboard, Key: 39) { Repeat = true },
            Control.A => new(BindingKind.Keyboard, Key: 13),
            Control.B => new(BindingKind.Keyboard, Key: 27),
            Control.LeftShoulder => new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
            Control.RightShoulder => new(BindingKind.Keyboard, Key: 9),
            _ => new(BindingKind.None)
        };
    }
    private void ChangeNavigation(bool active, Gamepad state, Buttons entering)
    {
        // A queued selection belongs to the input mode that produced it. Mouse
        // focus or a deliberate stick return must not retarget that old command.
        NavigationGeneration = unchecked(NavigationGeneration + 1);
        KeyboardNavigationActive = active;
        _navigationStickOriginX = active ? state.LeftX : (short)0;
        _navigationStickOriginY = active ? state.LeftY : (short)0;
        _navigationSuppressed |= state.Buttons & NavigationButtons & ~entering;
        _pressTime.Clear(); _repeat.Reset(); _chords.Reset();
        // Focus navigation inside Relay coexists with the pointer. Changing button
        // ownership must not discard an in-progress stick movement or its fractions.
        if (!RelayWindowActive)
        {
            _velocityX = _velocityY = _remainderX = _remainderY = _digitalX = _digitalY = 0;
            MotionLearning.ResetStroke();
        }
        Trace?.Invoke(active ? "MODE Keyboard navigation" : "MODE Mouse control");
    }
    public void NotifyPointerInteraction(bool clicked = false)
    {
        if (!RelayWindowActive) return;
        // A click can focus a different control even when pointer mode was
        // already active. Movement alone must not erase accepted focus steps.
        if (clicked) InvalidateRelayNavigation();
        if (RelayWindowActive && KeyboardNavigationActive && !KeyboardOpen && !ConfigurationMode)
            ChangeNavigation(false, _physical.Current.Raw, Buttons.None);
    }
    private bool FreshRelayPointerGesture(Gamepad state)
    {
        // Pointer movement retains the profile dead zone. Returning button
        // ownership requires a new displacement by that dead-zone distance from
        // the position at D-pad entry, not an already-held stick or its jitter.
        // The four-percent lower neutral threshold provides hysteresis before
        // rearming a gesture from centre; this does not delay or debounce input.
        double neutralThreshold = Math.Max(.01, _settings.DeadZone - .04);
        if (Shape(state.LeftX, state.LeftY, neutralThreshold, 1) == (0d, 0d))
        {
            _navigationStickOriginX = _navigationStickOriginY = 0;
            return false;
        }
        if (Shape(state.LeftX, state.LeftY, _settings.DeadZone, 1) == (0d, 0d)) return false;
        double dx = ((int)state.LeftX - _navigationStickOriginX) / 32768d;
        double dy = ((int)state.LeftY - _navigationStickOriginY) / 32768d;
        return dx * dx + dy * dy > _settings.DeadZone * _settings.DeadZone;
    }
    public AdaptiveMotion MotionLearning { get; } = new();
    private readonly ButtonChordRouter _chords = new();
    public (double Smoothing, double Speed) AppliedMotion { get; private set; } = (0, 1);
    private bool _connected, _armed;
    private readonly InputStateManager _physical = new(), _effective = new();
    public InputState PhysicalState => _physical.Current;
    public InputState EffectiveState => _effective.Current;
    public Action<string>? Trace { get; set; }
    private readonly HashSet<Shortcut> _frameActions = [];
    private double _velocityX, _velocityY, _remainderX, _remainderY;
    private double _digitalX, _digitalY;
    private readonly ScrollProcessor _scroll = new();
    public bool ScrollActive => _scroll.Active;
    public void ResetScroll() => _scroll.Reset();
    private readonly HashSet<MouseButton> _held = [];
    private readonly Dictionary<MouseButton, double> _pressTime = [];
    private readonly RepeatScheduler _repeat = new();

    public bool Enabled { get; private set; } = true;
    public bool Armed => _armed;
    // Ownership protects actions such as Select/Back from being retargeted after
    // mouse control resumes. Focus steps survive that button-mode transition;
    // lifecycle resets and actual mouse clicks invalidate both generations.
    public long NavigationGeneration { get; private set; }
    public long FocusNavigationGeneration { get; private set; }
    public long CaptureRelayNavigationGeneration(RelayNavigationCommand command) => command is
        RelayNavigationCommand.Up or RelayNavigationCommand.Down or RelayNavigationCommand.Left or RelayNavigationCommand.Right or
        RelayNavigationCommand.Next or RelayNavigationCommand.Previous ? FocusNavigationGeneration : NavigationGeneration;
    public bool IsRelayNavigationCurrent(RelayNavigationCommand command, long generation) =>
        CaptureRelayNavigationGeneration(command) == generation;
    public void InvalidateRelayNavigation()
    {
        NavigationGeneration = unchecked(NavigationGeneration + 1);
        FocusNavigationGeneration = unchecked(FocusNavigationGeneration + 1);
    }
    public Settings Settings { get => _settings; set { Reset(); _settings = value.Validate(); } }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Reset();
    }

    public void Reset()
    {
        InvalidateRelayNavigation();
        foreach (var button in _held.ToArray())
        {
            sink.SetMouseButton(button, false);
            _held.Remove(button);
        }
        _pressTime.Clear();
        KeyboardNavigationActive = false; _navigationSuppressed = Buttons.None;
        _navigationStickOriginX = _navigationStickOriginY = 0;
        _physical.Reset(); _effective.Reset();
        _pausedNavigationActive = false;
        _armed = false;
        _velocityX = _velocityY = _remainderX = _remainderY = 0;
        _digitalX = _digitalY = 0;
        _scroll.Reset();
        MotionLearning.ResetStroke();
        _chords.Reset(); AppliedMotion = (0, 1);
        _repeat.Reset(); _frameActions.Clear();
        if (sink is IRelayInputCleanup cleanup) cleanup.ReleaseAll();
    }

    public void Process(Gamepad? input, double elapsedSeconds)
    {
        if (input is not { } state)
        {
            Reset();
            _connected = false;
            return;
        }
        if (!_connected) { Reset(); _connected = true; }
        bool pausedNavigation = CanNavigateWhilePaused;
        if (pausedNavigation != _pausedNavigationActive)
        {
            // A paused window change invalidates queued commands and held inputs.
            // A neutral frame outside Relay cannot arm a newly active editor.
            Reset(); _pausedNavigationActive = pausedNavigation;
        }
        var physical = _physical.Update(state);
        if (Trace != null)
            foreach (var option in ControlMapping.Controls)
                if (physical.WasPressed(option.Control) || physical.WasReleased(option.Control))
                    Trace($"INPUT {option.Control} {(physical.IsDown(option.Control) ? "DOWN rising" : "UP falling")}");
        var dt = double.IsFinite(elapsedSeconds) ? Math.Clamp(elapsedSeconds, 0, 0.05) : 0;
        if (ConfigurationMode && !RelayWindowActive) { Reset(); return; }
        if (!_armed)
        {
            _armed = IsNeutral(state);
            return;
        }

        const Buttons chord = Buttons.Start | Buttons.Back;
        if ((state.Buttons & chord) == chord)
        {
            SetEnabled(!Enabled);
            return; // Re-arm only after the whole controller returns to neutral.
        }
        if (!Enabled && !pausedNavigation) return;

        if (ConfigurationMode || pausedNavigation)
        {
            // Configuring Relay never moves the desktop pointer or invokes a mapped Windows action.
            // Focus leaving Relay also discards repeats and requires neutral before navigation resumes.
            if (!RelayWindowActive) { Reset(); return; }
            KeyboardNavigationActive = true;
            state = state with { LeftX = 0, LeftY = 0, RightX = 0, RightY = 0 };
            _frameActions.Clear();
            ProcessBindings(_effective.Update(state), dt, elapsedSeconds);
            return;
        }

        var physicalState = state;
        const Buttons directions = Buttons.Up | Buttons.Down | Buttons.Left | Buttons.Right;
        if (KeyboardOpen)
        {
            bool selecting = FixedSwitchKeyboardLayout || (state.Buttons & directions) != 0 ? true :
                Shape(state.LeftX, state.LeftY, Math.Min(.95, _settings.DeadZone + .12), 1) != (0d, 0d) ? false : KeyboardSelectionActive;
            if (selecting != KeyboardSelectionActive) { ChangeNavigation(false, state, selecting ? state.Buttons & directions : Buttons.None); KeyboardSelectionActive = selecting; }
        }
        bool navigationAvailable = !KeyboardOpen && (NarratorRunning || RelayWindowActive) && _settings.NarratorNavigation;
        bool pointerGesture = KeyboardNavigationActive && (RelayWindowActive ? FreshRelayPointerGesture(state) :
            Shape(state.LeftX, state.LeftY, Math.Min(.95, _settings.DeadZone + .12), 1) != (0d, 0d));
        if (KeyboardNavigationActive && (!navigationAvailable ||
            (state.Buttons & directions) == 0 && pointerGesture))
            ChangeNavigation(false, state, Buttons.None);
        else if (!KeyboardNavigationActive && navigationAvailable && (state.Buttons & directions) != 0)
            ChangeNavigation(true, state, state.Buttons & directions);
        _navigationSuppressed &= state.Buttons;
        state = state with { Buttons = state.Buttons & ~_navigationSuppressed };
        if (KeyboardNavigationActive && !RelayWindowActive || KeyboardOpen && KeyboardSelectionActive)
            state = state with { LeftX = 0, LeftY = 0, RightX = 0, RightY = 0 };
        _frameActions.Clear();
        state = _chords.Route(state, KeyboardOpen && KeyboardSelectionActive ? [] : _settings.ButtonChords ?? [], _settings.ChordWindowMilliseconds / 1000, dt,
            binding => { Trace?.Invoke($"MAP Combination -> {Describe(binding)}"); Execute(binding); });
        var effective = _effective.Update(state);
        ProcessBindings(effective, dt, elapsedSeconds);
        // Keep trigger-bound modifiers stable in the same hysteresis band as other trigger actions.
        var modifierState = state with
        {
            LeftTrigger = effective.IsDown(Control.LeftTrigger) ? (byte)255 : (byte)0,
            RightTrigger = effective.IsDown(Control.RightTrigger) ? (byte)255 : (byte)0
        };

        var (x, y) = Shape(state.LeftX, state.LeftY, _settings.DeadZone, _settings.ResponseCurve);
        double multiplier = ControlMapping.Modifier(modifierState, _settings, BindingKind.Precision) ? 0.28
            : ControlMapping.Modifier(modifierState, _settings, BindingKind.Boost) ? 1.8 : 1;
        double magnitude = Math.Sqrt(x * x + y * y);
        var raw = Shape(state.LeftX, state.LeftY, _settings.DeadZone, 1);
        bool excluded = physicalState.Buttons != Buttons.None || physicalState.LeftTrigger >= 35 || physicalState.RightTrigger >= 35 ||
            Shape(state.RightX, state.RightY, _settings.ScrollDeadZone, 1) != (0d, 0d) || _held.Count > 0;
        MotionLearning.Observe(raw.X, raw.Y, elapsedSeconds, _settings.MotionLearning, excluded);
        var adaptation = MotionLearning.Adjustment(_settings.MotionLearning, Math.Sqrt(raw.X * raw.X + raw.Y * raw.Y), excluded);
        AppliedMotion = adaptation;
        multiplier *= adaptation.Speed;
        // Stronger damping near fine movements; deliberate large movements remain responsive.
        double timeConstant = _settings.Smoothing + adaptation.Smoothing + (_settings.TremorStabilization
            ? _settings.StabilizationStrength * .18 * (1 - .55 * magnitude) : 0);
        double blend = timeConstant <= 0 ? 1 : 1 - Math.Exp(-dt / timeConstant);
        if (x == 0 && y == 0)
        {
            _velocityX = _velocityY = _remainderX = _remainderY = 0;
        }
        else
        {
            // Time-correct integration of a first-order filter, even at uneven polling intervals.
            double targetX = x * _settings.PointerSpeed * multiplier;
            double targetY = -y * _settings.PointerSpeed * multiplier;
            double integral = timeConstant * blend;
            _remainderX += targetX * dt + (_velocityX - targetX) * integral;
            _remainderY += targetY * dt + (_velocityY - targetY) * integral;
            _velocityX += (targetX - _velocityX) * blend;
            _velocityY += (targetY - _velocityY) * blend;
            int mx = TakeWhole(ref _remainderX), my = TakeWhole(ref _remainderY);
            if (mx != 0 || my != 0) sink.Move(mx, my);
        }

        int digitalX = 0, digitalY = 0;
        foreach (var option in ControlMapping.Controls)
        {
            var binding = ResolveBinding(option.Control);
            if (binding.Kind != BindingKind.Pointer || !ControlMapping.Down(modifierState, option.Control)) continue;
            digitalX += binding.Key == 37 ? -1 : binding.Key == 39 ? 1 : 0;
            digitalY += binding.Key == 38 ? -1 : binding.Key == 40 ? 1 : 0;
        }
        digitalX = Math.Sign(digitalX); digitalY = Math.Sign(digitalY);
        if (digitalX == 0 && digitalY == 0) _digitalX = _digitalY = 0;
        else
        {
            double scale = _settings.DpadSpeed * dt / Math.Sqrt(digitalX * digitalX + digitalY * digitalY);
            if (digitalX == 0) _digitalX = 0; else _digitalX += digitalX * scale;
            if (digitalY == 0) _digitalY = 0; else _digitalY += digitalY * scale;
            int dx = TakeWhole(ref _digitalX), dy = TakeWhole(ref _digitalY);
            if (dx != 0 || dy != 0) sink.Move(dx, dy);
        }
        var scroll = _scroll.Process(modifierState, _settings, dt);
        if (scroll.Vertical != 0 || scroll.Horizontal != 0) sink.Scroll(scroll.Vertical, scroll.Horizontal);
    }

    private bool IsNeutral(Gamepad state) => state.Buttons == Buttons.None && state.LeftTrigger < 35 &&
        state.RightTrigger < 35 && Shape(state.LeftX, state.LeftY, _settings.DeadZone, 1) == (0d, 0d) &&
        Shape(state.RightX, state.RightY, Math.Max(.01, _settings.ScrollDeadZone - .04), 1) == (0d, 0d);

    public static (double X, double Y) Shape(short rawX, short rawY, double deadZone, double curve)
    {
        double x = rawX / (rawX < 0 ? 32768d : 32767d);
        double y = rawY / (rawY < 0 ? 32768d : 32767d);
        double magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= deadZone) return (0, 0);
        double scaled = Math.Pow((Math.Min(1, magnitude) - deadZone) / (1 - deadZone), curve);
        return (x / magnitude * scaled, y / magnitude * scaled);
    }

    private static int TakeWhole(ref double value)
    {
        int whole = (int)value;
        value -= whole;
        return whole;
    }

    private void Hold(bool down, MouseButton target, double dt)
    {
        if (!down) _pressTime[target] = 0;
        else
        {
            _pressTime[target] = _pressTime.GetValueOrDefault(target) + dt;
            if (_pressTime[target] * 1000 + .0001 < _settings.ClickHoldMilliseconds) return;
        }
        if (down == _held.Contains(target)) return;
        sink.SetMouseButton(target, down);
        if (down) _held.Add(target); else _held.Remove(target);
    }

    public static string Describe(ControlBinding binding) => $"{RelayActions.Category(binding)}." +
        (binding.Kind switch
        {
            BindingKind.Shortcut => RelayActions.Name(binding.Shortcut),
            BindingKind.Mouse => binding.Mouse + "Button",
            BindingKind.Pointer => "Move" + (binding.Key switch { 37 => "Left", 38 => "Up", 39 => "Right", _ => "Down" }),
            _ => binding.Kind.ToString()
        }) + (binding.Repeat ? " (repeat)" : "");
    private void Execute(ControlBinding binding)
    {
        if (RelayOwnsNavigation && RelayNavigationRequested is { } navigate && TryRelayNavigation(binding, out var command))
        {
            navigate(command);
            return; // Relay owns this focus action; never inject a second Windows key.
        }
        if (!Enabled) return; // Paused navigation has no native-output fallback.
        if (binding.Kind == BindingKind.Shortcut)
        {
            if (RelayActions.Category(binding.Shortcut) == ActionCategory.Accessibility && !_frameActions.Add(binding.Shortcut))
            { Trace?.Invoke($"ACTION {binding.Shortcut} duplicate in frame skipped"); return; }
            sink.Invoke(binding.Shortcut);
        }
        else if (binding.Kind == BindingKind.Keyboard) sink.InvokeKeys(binding.Keys());
    }
    private static bool TryRelayNavigation(ControlBinding binding, out RelayNavigationCommand command)
    {
        command = binding.Kind == BindingKind.Shortcut ? binding.Shortcut switch
        {
            Shortcut.Up => RelayNavigationCommand.Up,
            Shortcut.Down => RelayNavigationCommand.Down,
            Shortcut.Left => RelayNavigationCommand.Left,
            Shortcut.Right => RelayNavigationCommand.Right,
            _ => (RelayNavigationCommand)(-1)
        } : binding.Kind == BindingKind.Keyboard &&
            (binding.Modifiers == 0 || binding.Key == 9 && binding.Modifiers == 4) ? binding.Key switch
            {
                38 => RelayNavigationCommand.Up,
                40 => RelayNavigationCommand.Down,
                37 => RelayNavigationCommand.Left,
                39 => RelayNavigationCommand.Right,
                9 => binding.Modifiers == 4 ? RelayNavigationCommand.Previous : RelayNavigationCommand.Next,
                13 => RelayNavigationCommand.Select,
                27 => RelayNavigationCommand.Back,
                _ => (RelayNavigationCommand)(-1)
            } : (RelayNavigationCommand)(-1);
        return (int)command >= 0;
    }
    private bool RelayOwnsNavigation => RelayWindowActive &&
        (CanNavigateWhilePaused || !(KeyboardOpen && KeyboardSelectionActive && !ConfigurationMode));
    private void ProcessBindings(InputState state, double dt, double elapsed)
    {
        var mouse = new HashSet<MouseButton>();
        foreach (var option in ControlMapping.Controls)
        {
            var c = option.Control;
            bool down = state.IsDown(c), pressed = state.WasPressed(c), released = state.WasReleased(c);
            var binding = ResolveBinding(c);
            bool onRelease = c is Control.Back or Control.Start;
            if (binding.Kind == BindingKind.Mouse && !onRelease)
            {
                if (down) mouse.Add(binding.Mouse);
                if (pressed || released) Trace?.Invoke($"MAP {c} -> Pointer.{binding.Mouse} {(down ? "hold" : "release")}");
                continue;
            }
            bool fire = onRelease ? released : pressed;
            bool relayNavigation = RelayOwnsNavigation && TryRelayNavigation(binding, out _);
            if (!onRelease && !relayNavigation && (binding.Repeat || binding.Kind == BindingKind.Shortcut && (RelayActions.IsArrow(binding.Shortcut) || KeyboardOpen && binding.Shortcut is Shortcut.KeyboardUp or Shortcut.KeyboardDown or Shortcut.KeyboardLeft or Shortcut.KeyboardRight or Shortcut.KeyboardBackspace)))
            {
                fire = _repeat.ShouldFire(c, down, pressed, elapsed, _settings.RepeatDelayMilliseconds / 1000,
                    _settings.RepeatIntervalMilliseconds / 1000);
            }
            if (!fire) continue;
            Trace?.Invoke($"MAP {c} -> {Describe(binding)} {(pressed ? "pressed" : released ? "released" : "repeat")}");
            if (binding.Kind is BindingKind.Shortcut or BindingKind.Keyboard) Execute(binding);
            else if (binding.Kind == BindingKind.Mouse && !_held.Contains(binding.Mouse))
            {
                sink.SetMouseButton(binding.Mouse, true); sink.SetMouseButton(binding.Mouse, false);
            }
        }
        // Aggregate owners: releasing one of two sources must not end the other's drag.
        foreach (var target in Enum.GetValues<MouseButton>()) Hold(mouse.Contains(target), target, dt);
    }
}

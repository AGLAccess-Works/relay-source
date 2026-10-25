using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal sealed record ControllerStatus(bool Connected, bool Enabled, bool Armed, int Slot, string Battery,
    Gamepad State, string? Warning, bool Suspended);

internal sealed class ControllerService : IDisposable
{
    private readonly object _gate = new();
    private readonly ControllerIdentityObserver _identity;
    private readonly ControllerSelection _selection = new();
    private int[] _connectedSlots = [];
    public IReadOnlyList<ControllerDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                var inventory = _identity.Snapshot;
                return _selection.Describe(_processor.Settings.ControllerType, inventory.Devices, inventory.Slots);
            }
        }
    }
    public ControllerKind ControllerKind
    {
        get
        {
            lock (_gate)
            {
                return Devices.FirstOrDefault(device => device.Selected)?.Kind ?? ControllerKind.Unknown;
            }
        }
    }
    public string ControllerModel
    {
        get
        {
            lock (_gate)
            {
                if (_slot < 0) return "No controller connected";
                var inventory = _identity.Snapshot;
                var kind = ControllerKind;
                string source = _processor.Settings.ControllerType == ControllerPreference.Automatic ? "Detected" : "Chosen";
                return kind == ControllerKind.Adaptive ? source + ": Xbox Adaptive Controller" :
                    kind == ControllerKind.Standard ? source + ": Xbox controller" :
                    inventory.Devices.Length > 1 || _selection.Slots.Count > 1 ? "Multiple controllers connected; selected controller model is not identified" :
                    "Xbox-compatible controller; model not identified";
            }
        }
    }
    public string ControllerSetupHint => ControllerKind == ControllerKind.Adaptive
        ? "Adaptive Controller: your switches use their assigned Xbox buttons. Choose one button at a time in setup if combinations are difficult. Relay cannot identify accessories plugged into individual ports."
        : "All buttons can be mapped. Choose Adaptive below if Windows does not identify your controller.";
    private readonly WindowsInputSink _sink;
    private readonly bool _verificationOutput;
    public InputDiagnostics Diagnostics { get; } = new();
    private bool _disposed, _accessoryCapture;
    private readonly HashSet<IntPtr> _pausedNavigationWindows = [];
    internal void RegisterPausedNavigationWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        lock (_gate)
        {
            if (_disposed) return;
            NativeMethods.GetWindowThreadProcessId(handle, out uint owner);
            if (owner == (uint)Environment.ProcessId) _pausedNavigationWindows.Add(handle);
        }
    }
    internal void UnregisterPausedNavigationWindow(IntPtr handle)
    {
        lock (_gate)
        {
            if (!_pausedNavigationWindows.Remove(handle)) return;
            if (handle == _scrollWindow)
            {
                _processor.PausedNavigationWindowActive = false;
                _processor.Reset();
            }
        }
    }
    internal bool OwnsPausedNavigationWindow(IntPtr handle)
    {
        lock (_gate)
        {
            if (_disposed || handle == IntPtr.Zero || !_pausedNavigationWindows.Contains(handle)) return false;
            NativeMethods.GetWindowThreadProcessId(handle, out uint owner);
            return owner == (uint)Environment.ProcessId;
        }
    }
    internal void ProcessVerificationInput(Gamepad? input, IntPtr foreground)
    {
        if (System.Windows.Application.Current is not App { SmokeTest: true } || !_verificationOutput || _worker != null)
            throw new InvalidOperationException("Synthetic controller input requires the isolated preview and a fake output sink.");
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ControllerService));
            NativeMethods.GetWindowThreadProcessId(foreground, out uint owner);
            _processor.RelayWindowActive = owner == (uint)Environment.ProcessId;
            _processor.PausedNavigationWindowActive = OwnsPausedNavigationWindow(foreground);
            if (foreground != _scrollWindow)
            {
                _scrollWindow = foreground;
                if (!_processor.Enabled) _processor.Reset(); else _processor.InvalidateRelayNavigation();
            }
            if (_sessionLocked || _powerSuspended || _accessoryCapture || _sink.CleanupPending) _processor.Reset();
            else _processor.Process(input, .008);
            _status = new(input.HasValue, _processor.Enabled, _processor.Armed, input.HasValue ? 0 : -1,
                "Isolated controller preview", input ?? default, _sink.Warning, _sessionLocked || _powerSuspended);
        }
    }
    public void SetAccessoryCapture(bool capture)
    {
        lock (_gate)
        {
            _accessoryCapture = capture; _processor.Reset(); _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration();
            _status = _status with { Armed = false };
        }
    }
    private readonly InputProcessor _processor;
    private readonly MotionLearningStore _motionStore;
    private string _profileId;
    private Gamepad _lastDiagnosticPad;
    private double _nextDiagnosticSample;
    public string MotionSummary
    {
        get
        {
            lock (_gate)
            {
                var model = _processor.MotionLearning.Model;
                string mode = _processor.Settings.MotionLearning switch { LearningMode.Learn => "Learning", LearningMode.Frozen => "Frozen", _ => "Off" };
                var learning = _processor.MotionLearning;
                string progress = learning.ObservedSeconds < 5 ? $"{learning.ObservedSeconds:0.0} / 5 seconds collected" : $"{learning.ObservedSeconds:0} seconds collected";
                string pending = Math.Abs(learning.PendingSmoothing - model.Smoothing) >= .0005 ? " Release the stick to apply the next adjustment." : "";
                string explanation = model.Seconds >= 5 && model.Smoothing < .0005 && learning.PendingSmoothing < .0005 ? " No additional smoothing needed for the movement observed so far." : "";
                return $"{mode} · {progress}. {learning.Activity}. Saved: {model.Smoothing * 1000:0} ms smoothing. Applied now: {_processor.AppliedMotion.Smoothing * 1000:0} ms / {(1 - _processor.AppliedMotion.Speed) * 100:0}% slowdown." + pending + explanation + " " + (_motionStore.Warning ?? "Stored only on this PC.");
            }
        }
    }
    public void ForgetMotion() { lock (_gate) { _processor.MotionLearning.Forget(); _processor.Reset(); _motionStore.Put(_profileId, new()); _motionStore.Save(); } }
    public void SaveMotion(System.Collections.Generic.IEnumerable<string> profileIds)
    {
        lock (_gate) { _motionStore.Put(_profileId, _processor.MotionLearning.Model); _motionStore.Retain(profileIds); _motionStore.Save(); }
    }
    internal WindowsTargetAssistant Targets { get; }
    public event Action? AccessibilityHubRequested;
    public event Action? KeyboardRequested;
    public event Action<Shortcut>? KeyboardCommandRequested;
    public event Action<RelayNavigationCommand>? RelayNavigationRequested;
    public bool CanNavigateRelayUi { get { lock (_gate) return CanNavigateRelayUiCore; } }
    public long CaptureRelayNavigationGeneration() { lock (_gate) return _processor.NavigationGeneration; }
    public long CaptureRelayNavigationGeneration(RelayNavigationCommand command) { lock (_gate) return _processor.CaptureRelayNavigationGeneration(command); }
    public bool IsRelayNavigationCurrent(long generation)
    {
        lock (_gate) return
        CanNavigateRelayUiCore && generation == _processor.NavigationGeneration;
    }
    public bool IsRelayNavigationCurrent(RelayNavigationCommand command, long generation)
    {
        lock (_gate) return
        CanNavigateRelayUiCore && _processor.IsRelayNavigationCurrent(command, generation);
    }
    public bool TryNavigateNativeDialog(RelayNavigationCommand command, IntPtr expectedHwnd, long generation)
    {
        lock (_gate)
        {
            // Only the caller's same-process native dialog may receive this safe
            // navigation tap. The sink retains ownership if a native key-up fails.
            bool sent = NativeDialogNavigation.TryNavigate(command, expectedHwnd,
                () => _processor.Enabled && CanNavigateRelayUiCore && _processor.IsRelayNavigationCurrent(command, generation),
                NativeMethods.GetForegroundWindow,
                window => { NativeMethods.GetWindowThreadProcessId(window, out uint owner); return owner; }, _sink);
            if (_sink.CleanupPending) _processor.InvalidateRelayNavigation();
            return sent;
        }
    }
    private bool CanNavigateRelayUiCore => !_disposed && !_sessionLocked && !_powerSuspended && !_accessoryCapture &&
        (_processor.Enabled || _processor.CanNavigateWhilePaused) && _processor.Armed && _processor.RelayWindowActive && !_sink.CleanupPending &&
        (_processor.CanNavigateWhilePaused || !(_processor.KeyboardOpen && _processor.KeyboardSelectionActive && !_processor.ConfigurationMode));
    public void NotifyPointerInteraction(bool clicked = false) { lock (_gate) { if (!_disposed) _processor.NotifyPointerInteraction(clicked); } }
    public bool KeyboardSelectionActive { get { lock (_gate) return _processor.KeyboardOpen && _processor.KeyboardSelectionActive; } }
    public bool FixedSwitchKeyboardLayout { get { lock (_gate) return _processor.FixedSwitchKeyboardLayout; } }
    public void SetKeyboardOpen(bool open) { lock (_gate) { _processor.SetKeyboardOpen(open); _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration(); } }
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;
    private Task? _narratorObserver;
    private volatile bool _narratorRunning;
    public ControlBinding ResolveBinding(Control control) { lock (_gate) return _processor.ResolveBinding(control); }
    public bool KeyboardNavigationActive { get { lock (_gate) return _processor.KeyboardNavigationActive; } }
    private async Task ObserveNarratorAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            do
            {
                try { _narratorRunning = AccessibilityActionExecutor.RunningInSession("Narrator"); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { _narratorRunning = false; }
            } while (await timer.WaitForNextTickAsync(_cancel.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { }
    }
    private int _slot = -1;
    private double _nextProbe, _nextBattery, _lastMove = -10, _lastScroll = -10;
    private readonly HapticScheduler _haptics = new();
    private readonly WindowsNavigationObserver _navigation = new();
    private readonly HapticDevice _hapticDevice = new();
    private bool _scrollAcknowledged;
    private int _vertical, _horizontal;
    private IntPtr _scrollWindow;
    private string _feedbackSummary = "Feedback is ready when you are.";
    public string FeedbackSummary { get { lock (_gate) return _feedbackSummary; } }
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private bool _sessionLocked, _powerSuspended;
    private string _battery = "Unavailable";
    private ControllerStatus _status = new(false, true, false, -1, "Unavailable", default, null, false);

    public ControllerService(Settings settings, string? memoryPath = null, string profileId = "personal", WindowsInputSink? verificationSink = null)
    {
        _identity = new(() => Volatile.Read(ref _connectedSlots));
        if (verificationSink != null && (verificationSink.UsesNativeInput || System.Windows.Application.Current is not App { SmokeTest: true }))
            throw new InvalidOperationException("A supplied controller output sink is limited to the isolated verification preview.");
        _verificationOutput = verificationSink is { UsesNativeInput: false };
        _sink = verificationSink ?? new(diagnostics: Diagnostics);
        _processor = new InputProcessor(_sink) { Settings = settings };
        _processor.RelayNavigationRequested = command =>
        {
            // A preceding binding in this same frame may have left an owned key
            // awaiting release. Internal UI actions must respect that quarantine too.
            if (!CanNavigateRelayUiCore) return;
            RelayNavigationRequested?.Invoke(command);
        };
        _processor.Trace = detail => Diagnostics.Write(DiagnosticLevel.Trace, "STATE / MAP", detail);
        _profileId = profileId;
        _motionStore = new MotionLearningStore(memoryPath == null ? null : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(memoryPath)!, "motion-learning.json"));
        _motionStore.Load(); _processor.MotionLearning.Load(_motionStore.Get(profileId));
        Targets = new WindowsTargetAssistant(memoryPath);
        Targets.Configure(settings);
        _sink.MovementTransform = Targets.Adjust;
        _sink.LeftClickDelivered = Targets.RecordClick;
        _sink.AccessibilityHubRequested = () => AccessibilityHubRequested?.Invoke();
        _sink.KeyboardRequested = () => KeyboardRequested?.Invoke();
        _sink.KeyboardCommandRequested = command => KeyboardCommandRequested?.Invoke(command);
        _sink.MagnifierInvoked = () => { if (_processor.Settings.MagnifierHaptics) RequestCue(HapticCue.Magnifier, Now); };
        _sink.ScrollDelivered = (v, h) =>
        {
            _lastScroll = Now; _vertical = Math.Sign(v); _horizontal = Math.Sign(h);
            Targets.ClearMotion();
            if (!_scrollAcknowledged && _processor.Settings.ScrollHaptics) RequestCue(HapticCue.Scroll, _lastScroll);
            _scrollAcknowledged = true;
        };
    }

    public string LastOutput { get { lock (_gate) return _sink.LastOutput; } }
    public ControllerStatus Status { get { lock (_gate) return _status; } }
    public bool ConfigurationMode { get { lock (_gate) return _processor.ConfigurationMode; } }
    public void SetConfigurationMode(bool enabled)
    {
        lock (_gate)
        {
            if (_disposed || _processor.ConfigurationMode == enabled) return;
            _processor.ConfigurationMode = enabled; _processor.Reset(); _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration();
            _status = _status with { Armed = false };
            Diagnostics.Write(DiagnosticLevel.Info, "SESSION", enabled ? "Configuration navigation; waiting for neutral" : "Configuration ended; waiting for neutral");
        }
    }
    public void RequestReconnect()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _nextProbe = 0;
            Diagnostics.Write(DiagnosticLevel.Info, "DEVICE", "Controller inventory refresh requested");
        }
    }
    public bool SelectController(int slot)
    {
        lock (_gate)
        {
            if (_disposed || slot is < 0 or > 3 || _sessionLocked || _powerSuspended) return false;
            // An explicit selection is checked against current hardware, not a stale UI snapshot.
            ProbeControllers(Now, out _);
            if (!_selection.Select(slot)) return false;
            if (_slot != slot) ChangeController(slot);
            return true;
        }
    }
    public void Start() { _identity.Start(); Targets.Start(); _navigation.Start(); _narratorObserver ??= Task.Run(ObserveNarratorAsync); _worker ??= Task.Run(RunAsync); }
    public void UpdateSettings(Settings settings, string? profileId = null)
    {
        lock (_gate)
        {
            if (profileId != null && profileId != _profileId)
            {
                _motionStore.Put(_profileId, _processor.MotionLearning.Model);
                _profileId = profileId; _processor.MotionLearning.Load(_motionStore.Get(profileId));
            }
            _processor.Settings = settings; _sink.ReleaseAll(); Targets.Configure(settings); StopVibration(); _navigation.Reset(); _scrollAcknowledged = false;
            Diagnostics.Write(DiagnosticLevel.Info, "PROFILE", "Settings applied; input reset; waiting for neutral.");
        }
    }
    public void InvokeAction(Shortcut action)
    {
        lock (_gate)
        {
            if (_disposed || _sessionLocked || _powerSuspended || _accessoryCapture) return;
            _sink.Invoke(action);
        }
    }
    public bool TypeKeys(ushort[] keys)
    {
        lock (_gate) return !_disposed && !_accessoryCapture && _processor.Enabled && !_sessionLocked && !_powerSuspended && _sink.TryInvokeKeys(keys);
    }
    public void Toggle()
    {
        lock (_gate)
        {
            _processor.SetEnabled(!_processor.Enabled);
            _sink.ReleaseAll(); StopVibration(); Targets.ClearMotion();
            _status = _status with { Enabled = _processor.Enabled, Armed = false };
            Diagnostics.Write(DiagnosticLevel.Info, "SESSION", _processor.Enabled ? "Resume; waiting for neutral" : "Pause; cleanup requested");
        }
    }

    public void Suspend(bool suspended, bool power)
    {
        lock (_gate)
        {
            if (power) _powerSuspended = suspended; else _sessionLocked = suspended;
            _processor.Reset(); _sink.ReleaseAll(); StopVibration(); Targets.ClearMotion();
            _nextProbe = 0;
            Diagnostics.Write(DiagnosticLevel.Info, "SESSION", $"{(power ? "Power" : "Windows session")} suspended={suspended}; cleanup requested");
        }
    }

    public void ReleaseAllRelayInputs()
    {
        lock (_gate)
        {
            _processor.Reset(); _sink.ReleaseAll(); StopVibration(); Targets.ClearMotion();
            _status = _status with { Armed = false };
            Diagnostics.Write(DiagnosticLevel.Info, "CLEANUP", "Developer release/reset; waiting for neutral. " + _sink.OwnershipSummary);
        }
    }
    public string DiagnosticSnapshot()
    {
        lock (_gate)
        {
            var raw = _processor.PhysicalState; var processed = _processor.EffectiveState;
            var shape = InputProcessor.Shape(_status.State.LeftX, _status.State.LeftY, _processor.Settings.DeadZone, _processor.Settings.ResponseCurve);
            return $"Device: XInput logical device; slot {_slot}; connected={_status.Connected}. Model: {ControllerModel}.\n" +
                $"Profile ID: {_profileId}; enabled={_processor.Enabled}; armed={_processor.Armed}; suspended={_sessionLocked || _powerSuspended}\n" +
                $"Raw: buttons={_status.State.Buttons}; LT={_status.State.LeftTrigger}; RT={_status.State.RightTrigger}; left=({_status.State.LeftX},{_status.State.LeftY}); right=({_status.State.RightX},{_status.State.RightY})\n" +
                $"Physical held=0x{raw.Held:X}; rising=0x{raw.Pressed:X}; falling=0x{raw.Released:X}\n" +
                $"Effective held=0x{processed.Held:X}; rising=0x{processed.Pressed:X}; falling=0x{processed.Released:X}; shaped left=({shape.X:0.000},{shape.Y:0.000}); scroll active={_processor.ScrollActive}\n" +
                $"{_sink.OwnershipSummary}\nLast action: {_sink.LastAction}; {_sink.LastActionMilliseconds:0.000} ms\nLast accepted output: {_sink.LastOutput}\nWarning: {_sink.Warning ?? "None"}\n" +
                string.Join("\n", ControlMapping.Controls.Select(o => $"{o.Control} -> {InputProcessor.Describe(ControlMapping.Resolve(_processor.Settings, o.Control))}"));
        }
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(8));
        double last = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        try
        {
            while (await timer.WaitForNextTickAsync(_cancel.Token).ConfigureAwait(false))
            {
                double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                lock (_gate) Tick(now, now - last);
                last = now;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Write(ex);
            lock (_gate)
            {
                _processor.SetEnabled(false);
                Diagnostics.Write(DiagnosticLevel.Error, "SESSION", "Input worker stopped: " + ex.GetType().Name + "; cleanup requested");
                _status = _status with { Enabled = false, Armed = false, Warning = "Controller service stopped. Restart the app; details are in the local log." };
            }
        }
        finally
        {
            lock (_gate) { _processor.Reset(); _sink.ReleaseAll(); StopVibration(); }
        }
    }

    private void Tick(double now, double dt)
    {
        _processor.NarratorRunning = _narratorRunning;
        bool suspended = _sessionLocked || _powerSuspended;
        ApplyVibration(_haptics.Sample(now));
        if (suspended)
        {
            _sink.ReleaseAll();
            _status = _status with { Suspended = true, Armed = false };
            return;
        }
        NativeMethods.XState state = default;
        if (now >= _nextProbe) ProbeControllers(now, out state);
        else if (_slot >= 0 && NativeMethods.GetState((uint)_slot, out state) != 0)
            ProbeControllers(now, out state); // Immediate safe fallback on disconnect; normal inventory refresh is once per second.
        if (_slot >= 0)
        {
            var window = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(window, out uint foregroundProcess);
            _processor.RelayWindowActive = foregroundProcess == (uint)Environment.ProcessId;
            _processor.PausedNavigationWindowActive = _processor.RelayWindowActive && OwnsPausedNavigationWindow(window);
            if (window != _scrollWindow) { _scrollWindow = window; if (!_processor.Enabled) _processor.Reset(); else _processor.InvalidateRelayNavigation(); _processor.ResetScroll(); _navigation.Reset(); StopVibration(); _scrollAcknowledged = false; _lastScroll = -10; }
            _sink.RetryReleases();
            var pad = state.Gamepad.ToGamepad();
            if (Diagnostics.Enabled && now >= _nextDiagnosticSample && pad != _lastDiagnosticPad)
            {
                _nextDiagnosticSample = now + .05; _lastDiagnosticPad = pad;
                Diagnostics.Write(DiagnosticLevel.Trace, "RAW", $"Buttons={pad.Buttons}; LT={pad.LeftTrigger}; RT={pad.RightTrigger}; left=({pad.LeftX},{pad.LeftY}); right=({pad.RightX},{pad.RightY})");
            }
            if (_sink.CleanupPending)
            {
                _processor.Reset(); _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration();
                _status = new(true, _processor.Enabled, false, _slot, _battery, pad, _sink.Warning, false);
                return; // Quarantine new actions until Relay-owned releases are accepted, then require neutral.
            }
            if (_accessoryCapture) { _status = new(true, _processor.Enabled, false, _slot, _battery, pad, "Accessory recording: desktop input is paused.", false); return; }
            bool moving = InputProcessor.Shape(pad.LeftX, pad.LeftY, _processor.Settings.DeadZone, 1) != (0d, 0d);
            Targets.BeginFrame(dt, _processor.Enabled && _processor.Armed && !_processor.ConfigurationMode, moving && !_processor.ConfigurationMode,
                ControlMapping.MouseActive(pad, _processor.Settings) || _sink.HasHeldInputs);
            _processor.Process(pad, dt);
            if (_sink.CleanupPending) _processor.InvalidateRelayNavigation();
            if (moving) _lastMove = now;
            if (!_processor.ScrollActive) _scrollAcknowledged = false;
            var settings = _processor.Settings;
            bool active = _processor.Enabled && _processor.Armed && !_processor.ConfigurationMode && settings.Haptics && settings.HapticStrength > 0;
            bool scrolling = _processor.ScrollActive && now - _lastScroll < .3;
            _navigation.Update(active && settings.TargetHaptics && !scrolling && now - _lastMove < .75 &&
                !ControlMapping.MouseActive(pad, _processor.Settings) && !_sink.HasHeldInputs,
                active && settings.BoundaryHaptics && scrolling, _vertical, _horizontal, now);
            var cue = _navigation.Take(now);
            if (active && cue.HasValue) RequestCue(cue.Value, now);
            ApplyVibration(_haptics.Sample(now));
            if (now >= _nextBattery)
            {
                _nextBattery = now + 30;
                _battery = NativeMethods.GetBattery((uint)_slot, 0, out var battery) == 0
                    ? battery.Type == 1 ? "Wired / USB" : battery.Type is 2 or 3
                        ? battery.Level switch { 0 => "Battery empty", 1 => "Battery low", 2 => "Battery medium", _ => "Battery full" }
                        : "Battery unavailable"
                    : "Battery unavailable";
            }
        }
        if (_slot < 0 || !_processor.Enabled || !_processor.Armed) { _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration(); _navigation.Reset(); _scrollAcknowledged = false; }
        _status = new(_slot >= 0, _processor.Enabled, _processor.Armed, _slot, _battery,
            _slot >= 0 ? state.Gamepad.ToGamepad() : default, _sink.Warning ?? _hapticDevice.Warning, false);
    }

    private void ProbeControllers(double now, out NativeMethods.XState selectedState)
    {
        _nextProbe = now + 1;
        var states = new NativeMethods.XState[4];
        var slots = new List<int>(4);
        for (uint slot = 0; slot < 4; slot++)
            if (NativeMethods.GetState(slot, out states[slot]) == 0) slots.Add((int)slot);
        _selection.UpdateConnectedSlots(slots);
        Volatile.Write(ref _connectedSlots, slots.ToArray());
        if (_slot != _selection.SelectedSlot) ChangeController(_selection.SelectedSlot);
        selectedState = _slot < 0 ? default : states[_slot];
    }

    private void ChangeController(int slot)
    {
        int previous = _slot;
        // Stop old-device feedback and release only Relay's owned output before changing slots.
        _processor.Process(null, 0); _sink.ReleaseAll(); Targets.ClearMotion(); StopVibration();
        _slot = slot; _battery = "Unavailable"; _nextBattery = 0;
        _lastMove = _lastScroll = -10; _scrollWindow = IntPtr.Zero; _scrollAcknowledged = false;
        _lastDiagnosticPad = default;
        _status = new(slot >= 0, _processor.Enabled, false, slot, _battery, default, _sink.Warning, false);
        Diagnostics.Write(DiagnosticLevel.Info, "DEVICE", $"XInput selection {previous} -> {slot}; owned output released; waiting for neutral");
    }

    private void RequestCue(HapticCue cue, double now)
    {
        var settings = _processor.Settings;
        if (!settings.Haptics || _slot < 0 || _sessionLocked || _powerSuspended || _accessoryCapture || _processor.ConfigurationMode ||
            !_processor.Enabled || !_processor.Armed) return;
        if (_haptics.Request(cue, now, settings.HapticStrength))
            _feedbackSummary = cue switch
            {
                HapticCue.Scroll => "Scroll input started.",
                HapticCue.Target => "Pointer is over an accessible control.",
                HapticCue.Boundary => "Scroll region reached its end.",
                _ => "Magnifier shortcut sent."
            };
    }
    public void PreviewHaptics(HapticCue cue)
    {
        lock (_gate)
        {
            if (_slot < 0 || !_processor.Enabled || !_processor.Armed)
                _feedbackSummary = "Connect your controller and resume control to preview feedback.";
            else if (!_processor.Settings.Haptics || _processor.Settings.HapticStrength == 0)
                _feedbackSummary = "Enable haptics and increase the strength to preview feedback.";
            else
            {
                _haptics.Stop();
                RequestCue(cue, Now);
                _feedbackSummary = cue switch
                {
                    HapticCue.Scroll => "Preview: scroll-start pulse.",
                    HapticCue.Target => "Preview: target-hover pulse.",
                    HapticCue.Boundary => "Preview: end-of-scroll double pulse.",
                    _ => "Preview: Magnifier confirmation."
                };
            }
        }
    }
    private void ApplyVibration(MotorOutput output)
    {
        _hapticDevice.Apply(_slot, output);
    }
    private void StopVibration()
    {
        _haptics.Stop(); ApplyVibration(default); _navigation.Reset();
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; _pausedNavigationWindows.Clear(); Diagnostics.Write(DiagnosticLevel.Info, "SESSION", "Shutdown; cleanup requested"); }
        _cancel.Cancel();
        _worker?.GetAwaiter().GetResult();
        lock (_gate) { _processor.Reset(); _sink.ReleaseAll(); StopVibration(); _motionStore.Put(_profileId, _processor.MotionLearning.Model); _motionStore.Save(); }
        _narratorObserver?.GetAwaiter().GetResult();
        _identity.Dispose(); Targets.Dispose(); _navigation.Dispose();
        _cancel.Dispose();
    }
}

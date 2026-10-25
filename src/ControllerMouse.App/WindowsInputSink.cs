using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal sealed class WindowsInputSink : IInputSink, IRelayInputCleanup
{
    private readonly object _gate = new();
    private readonly Func<NativeMethods.Input[], uint> _sendInput;
    private readonly Action<string> _openSettings;
    private readonly Func<ushort, bool> _keyDown;
    private readonly AccessibilityActionExecutor _accessibility;
    private readonly bool _native;
    internal bool UsesNativeInput => _native;
    private readonly InputDiagnostics? _diagnostics;
    private readonly HashSet<MouseButton> _heldButtons = [];
    private readonly HashSet<MouseButton> _pendingReleases = [];
    private readonly HashSet<ushort> _heldKeys = [];
    private bool _shortcutActive;
    private string _lastOutput = "Nothing sent";
    private string? _warning;
    private string _lastAction = "None";
    private double _lastActionMilliseconds;
    private static readonly ushort[] Modifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];

    internal WindowsInputSink(Func<NativeMethods.Input[], uint>? sendInput = null, Action<string>? openSettings = null,
        Func<ushort, bool>? keyDown = null, AccessibilityActionExecutor? accessibility = null, InputDiagnostics? diagnostics = null)
    {
        _native = sendInput == null;
        _sendInput = sendInput ?? (inputs => NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>()));
        _openSettings = openSettings ?? (uri => { using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); });
        _keyDown = keyDown ?? (_native ? key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0 : _ => false);
        // Fake native boundaries must not launch real AT processes during tests.
        _accessibility = accessibility ?? (_native ? new() : new(_ => false, _ => { }));
        _diagnostics = diagnostics;
    }
    public string LastOutput { get { lock (_gate) return _lastOutput; } }
    public string? Warning { get { lock (_gate) return _warning; } }
    public string LastAction { get { lock (_gate) return _lastAction; } }
    public double LastActionMilliseconds { get { lock (_gate) return _lastActionMilliseconds; } }
    public string OwnershipSummary { get { lock (_gate) return $"Relay keys: {_heldKeys.Count}; mouse buttons: {_heldButtons.Count}; pending mouse releases: {_pendingReleases.Count}"; } }
    public Action<int, int>? ScrollDelivered { get; set; }
    public Action? MagnifierInvoked { get; set; }
    public Action? AccessibilityHubRequested { get; set; }
    public Action? KeyboardRequested { get; set; }
    public Action<Shortcut>? KeyboardCommandRequested { get; set; }
    public Action? LeftClickDelivered { get; set; }
    public Func<int, int, (int X, int Y)>? MovementTransform { get; set; }
    public bool HasHeldInputs { get { lock (_gate) return _heldButtons.Count > 0 || _heldKeys.Count > 0; } }
    public bool CleanupPending { get { lock (_gate) return _heldKeys.Count > 0 || _pendingReleases.Count > 0; } }

    public void Move(int x, int y)
    {
        lock (_gate)
        {
            if (!ReadyForOutput()) return;
            if (MovementTransform != null) (x, y) = MovementTransform(x, y);
            if (x != 0 || y != 0)
            {
                bool accepted = Send(Mouse(0x0001, x, y));
                if (accepted) _lastOutput = "Pointer movement";
                if (_diagnostics?.Enabled == true) _diagnostics.Write(accepted ? DiagnosticLevel.Trace : DiagnosticLevel.Warning,
                    "OUTPUT", $"Pointer.Move delta=({x},{y}) {(accepted ? "accepted" : "failed")}");
            }
        }
    }
    public void Scroll(int vertical, int horizontal)
    {
        lock (_gate)
        {
            if (!ReadyForOutput()) return;
            int v = vertical != 0 && Send(Mouse(0x0800, data: vertical)) ? vertical : 0;
            int h = horizontal != 0 && Send(Mouse(0x1000, data: horizontal)) ? horizontal : 0;
            if (v != 0 || h != 0) { _lastOutput = "Scroll"; ScrollDelivered?.Invoke(v, h); }
            if (_diagnostics?.Enabled == true) _diagnostics.Write(DiagnosticLevel.Trace, "OUTPUT", $"Pointer.Scroll requested=({vertical},{horizontal}) accepted=({v},{h})");
        }
    }
    public void SetMouseButton(MouseButton button, bool down)
    {
        lock (_gate)
        {
            if (down && !ReadyForOutput()) return;
            if (down && (_pendingReleases.Contains(button) || _heldButtons.Contains(button))) return;
            uint flag = button switch
            {
                MouseButton.Left => down ? 0x0002u : 0x0004u,
                MouseButton.Right => down ? 0x0008u : 0x0010u,
                _ => down ? 0x0020u : 0x0040u
            };
            // An up with no Relay owner must never release another device's mouse button.
            if (!down && !_heldButtons.Contains(button)) return;
            if (down)
            {
                ushort key = button switch { MouseButton.Left => 1, MouseButton.Right => 2, _ => 4 };
                try { if (_keyDown(key)) { Warn("A mouse button is already held by another device; Relay did not take ownership."); return; } }
                catch (Exception ex) { Failure(ex); return; }
            }
            // Conservative ownership allows cleanup even if a native wrapper throws after submitting a down.
            if (down) _heldButtons.Add(button);
            bool success = Send(Mouse(flag));
            if (success)
            {
                if (down) { _lastOutput = button + " button down"; if (button == MouseButton.Left) LeftClickDelivered?.Invoke(); }
                else { _heldButtons.Remove(button); _pendingReleases.Remove(button); }
            }
            else if (down && !_lastSendThrew) _heldButtons.Remove(button);
            if (!success && _heldButtons.Contains(button)) _pendingReleases.Add(button);
            _diagnostics?.Write(success ? DiagnosticLevel.Trace : DiagnosticLevel.Warning, "OUTPUT", $"Mouse.{button} {(down ? "DOWN" : "UP")} {(success ? "accepted" : "failed")}");
        }
    }

    public void Invoke(Shortcut shortcut)
    {
        lock (_gate)
        {
            if (_shortcutActive) { Warn("An action is already executing; nested input was rejected."); return; }
            _shortcutActive = true;
            long started = Stopwatch.GetTimestamp();
            _lastAction = $"{RelayActions.Category(shortcut)}.{RelayActions.Name(shortcut)}";
            _diagnostics?.Write(DiagnosticLevel.Info, "ACTION", _lastAction + " START");
            bool success = false;
            try
            {
                ReleaseKeys();
                if (_heldKeys.Count != 0) { Warn("Relay is still releasing a previous shortcut; this action was skipped."); return; }
                if (AccessibilityCatalog.SettingsUri(shortcut) is string uri)
                { _openSettings(uri); _warning = null; _lastOutput = AccessibilityCatalog.Actions.First(a => a.Action == shortcut).Name; success = true; }
                else if (shortcut == Shortcut.AccessibilityHub) { AccessibilityHubRequested?.Invoke(); success = true; }
                else if (shortcut is >= Shortcut.KeyboardUp and <= Shortcut.KeyboardPrevious) { KeyboardCommandRequested?.Invoke(shortcut); success = true; }
                else if (shortcut == Shortcut.OnScreenKeyboard) { KeyboardRequested?.Invoke(); success = true; }
                else if (RelayActions.Category(shortcut) == ActionCategory.Accessibility)
                {
                    success = _accessibility.Execute(shortcut, SendKeys);
                    if (success && shortcut is Shortcut.MagnifierStart or Shortcut.NarratorStart or Shortcut.NarratorStop)
                        _lastOutput = _lastAction + " requested";
                }
                else success = SendKeys(AccessibilityCatalog.Keys(shortcut), shortcut.ToString());
                if (success && shortcut is Shortcut.ZoomIn or Shortcut.ZoomOut or Shortcut.CloseMagnifier or Shortcut.MagnifierStart)
                    MagnifierInvoked?.Invoke();
            }
            catch (Exception ex) { success = false; Failure(ex); }
            finally
            {
                ReleaseKeys(); _shortcutActive = false;
                _lastActionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                _diagnostics?.Write(success ? DiagnosticLevel.Info : DiagnosticLevel.Warning, "ACTION",
                    $"{_lastAction} {(success ? "COMPLETE" : "FAILED / SKIPPED")} {_lastActionMilliseconds:0.000} ms; owned keys {_heldKeys.Count}");
            }
        }
    }

    public void InvokeKeys(ushort[] keys) => TryInvokeKeys(keys);
    internal bool TryInvokeKeys(ushort[] keys)
    {
        lock (_gate)
        {
            if (_shortcutActive) { Warn("An action is already executing; nested input was rejected."); return false; }
            _shortcutActive = true;
            long started = Stopwatch.GetTimestamp(); _lastAction = "Keyboard.Tap";
            _diagnostics?.Write(DiagnosticLevel.Trace, "ACTION", "Keyboard.Tap START (key values omitted)");
            bool success = false;
            try { return success = SendKeys(keys, "Keyboard input"); }
            catch (Exception ex) { Failure(ex); return false; }
            finally
            {
                ReleaseKeys(); _shortcutActive = false; _lastActionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                _diagnostics?.Write(success ? DiagnosticLevel.Trace : DiagnosticLevel.Warning, "ACTION",
                    $"Keyboard.Tap {(success ? "COMPLETE" : "FAILED / SKIPPED")} {_lastActionMilliseconds:0.000} ms; owned keys {_heldKeys.Count}");
            }
        }
    }
    private bool SendKeys(ushort[] keys, string label)
    {
        ReleaseKeys();
        if (_heldKeys.Count != 0 || keys.Length == 0) return false;
        keys = keys.Distinct().ToArray();
        // Do not inherit external modifiers or release keys which another device already holds.
        if (Modifiers.Concat(keys).Distinct().Any(_keyDown))
        { Warn("Release keys held on another device before using this shortcut. No keys were injected."); return false; }
        var inputs = keys.Select(k => Keyboard(k, false)).Concat(keys.Reverse().Select(k => Keyboard(k, true))).ToArray();
        foreach (var key in keys) _heldKeys.Add(key);
        _diagnostics?.Write(DiagnosticLevel.Trace, "OUTPUT", $"Keyboard batch START {inputs.Length} events (key values omitted)");
        uint sent = _sendInput(inputs);
        if (sent > inputs.Length) throw new InvalidOperationException("Native injector returned an invalid event count.");
        _heldKeys.Clear();
        for (int i = 0; i < sent; i++)
        {
            var keyboard = inputs[i].Data.Keyboard;
            if ((keyboard.Flags & 2) != 0) _heldKeys.Remove(keyboard.Key); else _heldKeys.Add(keyboard.Key);
        }
        _diagnostics?.Write(sent == inputs.Length ? DiagnosticLevel.Trace : DiagnosticLevel.Warning, "OUTPUT", $"Keyboard batch accepted {sent}/{inputs.Length}; owned keys {_heldKeys.Count}");
        if (sent != inputs.Length) { WarnBlocked(); return false; }
        _warning = null; _lastOutput = label; return true;
    }

    public void ReleaseAll()
    {
        lock (_gate)
        {
            bool owned = HasHeldInputs;
            foreach (var button in _heldButtons.ToArray()) SetMouseButton(button, false);
            ReleaseKeys();
            if (owned) _diagnostics?.Write(DiagnosticLevel.Info, "CLEANUP", OwnershipSummary);
        }
    }
    public void ReleaseKeys()
    {
        lock (_gate)
        {
            // Release modifiers first to minimize contamination if subsequent cleanup is blocked.
            foreach (ushort key in _heldKeys.OrderByDescending(k => Modifiers.Contains(k)).ToArray())
                if (Send(Keyboard(key, true), preserveWarning: true)) _heldKeys.Remove(key);
        }
    }
    public void RetryReleases()
    {
        lock (_gate)
        {
            foreach (var button in _pendingReleases.ToArray()) SetMouseButton(button, false);
            ReleaseKeys();
        }
    }
    private bool _lastSendThrew;
    private bool ReadyForOutput()
    {
        if (!CleanupPending) return true;
        RetryReleases();
        if (!CleanupPending) return true;
        Warn("Relay is still releasing previous input. New output is paused until cleanup succeeds.");
        return false;
    }
    private bool Send(NativeMethods.Input input, bool preserveWarning = false)
    {
        _lastSendThrew = false;
        try
        {
            if (_sendInput([input]) == 1) { if (!preserveWarning) _warning = null; return true; }
            WarnBlocked();
        }
        catch (Exception ex) { _lastSendThrew = true; Failure(ex); }
        return false;
    }
    private void WarnBlocked() => Warn("Windows blocked input. Switch away from an administrator window or the secure desktop.");
    private void Warn(string message)
    {
        if (_warning != message) _diagnostics?.Write(DiagnosticLevel.Warning, "OUTPUT", message);
        _warning = message;
    }
    private void Failure(Exception ex)
    {
        Warn("Relay could not deliver an input action. Cleanup will be retried; see diagnostics.");
        _diagnostics?.Write(DiagnosticLevel.Error, "OUTPUT", ex.GetType().Name);
        if (_native) Log.Write(ex);
    }
    internal const uint RelayMouseMarker = 0x524C4159;
    private static NativeMethods.Input Mouse(uint flags, int x = 0, int y = 0, int data = 0) =>
        new() { Data = new() { Mouse = new() { Flags = flags, X = x, Y = y, Data = unchecked((uint)data), Extra = (UIntPtr)RelayMouseMarker } } };
    private static NativeMethods.Input Keyboard(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new()
        {
            Keyboard = new()
            {
                Key = key,
                Flags = (up ? 2u : 0u) |
            (key is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x5B or 0x5C or 0x5D or 0x6F or 0x90 ? 1u : 0u)
            }
        }
    };
}

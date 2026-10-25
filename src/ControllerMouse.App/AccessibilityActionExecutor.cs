using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ControllerMouse.Core;

namespace ControllerMouse.App;

/// <summary>Semantic AT operations. The caller serializes execution with all other Relay input.</summary>
internal sealed class AccessibilityActionExecutor
{
    private readonly Func<string, bool> _running;
    private readonly Action<string> _start;
    internal AccessibilityActionExecutor(Func<string, bool>? running = null, Action<string>? start = null)
    {
        _running = running ?? RunningInSession;
        _start = start ?? (name =>
        {
            using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, name + ".exe"))
            { UseShellExecute = true });
        });
    }
    internal static bool RunningInSession(string name)
    {
        using var current = Process.GetCurrentProcess();
        bool found = false;
        foreach (var process in Process.GetProcessesByName(name))
            using (process)
            {
                try { if (process.SessionId == current.SessionId) found = true; }
                catch (InvalidOperationException) { /* Process exited while being observed. */ }
            }
        return found;
    }
    internal bool Execute(Shortcut action, Func<ushort[], string, bool> keys)
    {
        string label = AccessibilityCatalog.Actions.FirstOrDefault(a => a.Action == action)?.Name ?? action.ToString();
        switch (action)
        {
            case Shortcut.MagnifierStart:
                if (!_running("Magnify")) _start("Magnify");
                return true;
            case Shortcut.NarratorStart:
                if (!_running("Narrator")) _start("Narrator");
                return true;
            case Shortcut.NarratorStop:
                return !_running("Narrator") || keys(AccessibilityCatalog.Keys(Shortcut.Narrator), label);
            default:
                return keys(AccessibilityCatalog.Keys(action), label);
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;

namespace ControllerMouse.App;

// Configuration windows opt in by using the normal keyboard-access helper.
// Typing keyboards and native dialogs are deliberately absent from this list.
internal static class PausedRelayWindowNavigation
{
    private static readonly ConditionalWeakTable<Window, Registration> Registrations = new();

    internal static void Attach(Window window)
    {
        if (window is KeyboardWindow) return;
        Registrations.GetValue(window, owner => new Registration(owner)).Refresh();
    }
    internal static void Detach(Window window)
    {
        if (Registrations.TryGetValue(window, out var registration)) registration.Dispose();
    }

    private sealed class Registration
    {
        private readonly Window _window;
        private ControllerService? _service;
        private IntPtr _handle;

        internal Registration(Window window)
        {
            _window = window;
            window.SourceInitialized += SourceInitialized;
            window.Closed += Closed;
            // This helper may be applied to an already-created configuration window.
            Register();
        }

        private void SourceInitialized(object? sender, EventArgs e) => Register();
        internal void Refresh() => Register();

        private void Register()
        {
            if (Application.Current is not App { Service: { } service }) return;
            if (_handle != IntPtr.Zero && _service != service)
            {
                _service?.UnregisterPausedNavigationWindow(_handle); _handle = IntPtr.Zero;
            }
            if (_handle != IntPtr.Zero) return;
            var handle = new WindowInteropHelper(_window).Handle;
            if (handle == IntPtr.Zero) return;
            _service = service;
            _handle = handle;
            service.RegisterPausedNavigationWindow(handle);
        }

        private void Closed(object? sender, EventArgs e) => Dispose();
        internal void Dispose()
        {
            _window.SourceInitialized -= SourceInitialized;
            _window.Closed -= Closed;
            if (_handle != IntPtr.Zero) _service?.UnregisterPausedNavigationWindow(_handle);
            _handle = IntPtr.Zero; _service = null;
            Registrations.Remove(_window);
        }
    }
}

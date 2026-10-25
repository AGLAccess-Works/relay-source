using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ControllerMouse.App;

// Used only by isolated, explicitly requested verification modes.
internal static class NativeVerificationFocus
{
    internal static async Task Acquire(Window window)
    {
        if (Application.Current is not App { SmokeTest: true }) throw new InvalidOperationException("Native fixture focus requires verification mode.");
        window.Show();
        var handle = new WindowInteropHelper(window).Handle;
        ShowWindow(handle, 5);
        int attempts = Environment.GetCommandLineArgs().Contains("--wait-for-test-focus") ? 3600 : 20;
        for (int attempt = 0; attempt < attempts && NativeMethods.GetForegroundWindow() != handle; attempt++)
        {
            window.Activate(); SetForegroundWindow(handle);
            // Respect Windows foreground policy. Interactive verification may wait
            // for the user to activate this window; it never types to another app.
            await Task.Delay(50);
        }
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (NativeMethods.GetForegroundWindow() != handle)
            throw new InvalidOperationException("Windows did not grant foreground focus to the owned verification window. No input was sent.");
    }
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr window, int command);
}

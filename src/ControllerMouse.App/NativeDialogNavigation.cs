using System;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal static class NativeDialogNavigation
{
    // The service holds its gate throughout this call. These native boundaries
    // are injectable so verification never needs to type into another window.
    internal static bool TryNavigate(RelayNavigationCommand command, IntPtr expectedHwnd, Func<bool> isCurrent,
        Func<IntPtr> foregroundWindow, Func<IntPtr, uint> windowProcess, WindowsInputSink sink)
    {
        if (expectedHwnd == IntPtr.Zero || sink.CleanupPending || !isCurrent() || foregroundWindow() != expectedHwnd ||
            windowProcess(expectedHwnd) != (uint)Environment.ProcessId) return false;
        ushort[] keys = command switch
        {
            RelayNavigationCommand.Up => [0x26],
            RelayNavigationCommand.Down => [0x28],
            RelayNavigationCommand.Left => [0x25],
            RelayNavigationCommand.Right => [0x27],
            RelayNavigationCommand.Next => [0x09],
            RelayNavigationCommand.Previous => [0x10, 0x09],
            RelayNavigationCommand.Select => [0x0D],
            RelayNavigationCommand.Back => [0x1B],
            _ => []
        };
        if (keys.Length == 0 || sink.CleanupPending || !isCurrent() || foregroundWindow() != expectedHwnd) return false;
        return sink.TryInvokeKeys(keys);
    }
}

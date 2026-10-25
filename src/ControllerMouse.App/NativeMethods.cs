using System;
using System.Runtime.InteropServices;
using System.Text;
using ControllerMouse.Core;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace ControllerMouse.App;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct XState { public uint Packet; public XGamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct XGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
        public readonly Gamepad ToGamepad() => new((Buttons)Buttons, LeftTrigger, RightTrigger, LeftX, LeftY, RightX, RightY);
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Vibration { public ushort Left, Right; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Battery { public byte Type, Level; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetMessageExtraInfo();
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ToUnicodeEx(uint key, uint scan,
        byte[] state, StringBuilder text, int length, uint flags, IntPtr layout);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    internal static extern uint GetState(uint index, out XState state);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    internal static extern uint SetVibration(uint index, ref Vibration vibration);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    internal static extern uint GetBattery(uint index, byte deviceType, out Battery battery);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

using System;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal sealed class HapticDevice
{
    private readonly Func<uint, MotorOutput, uint> _send;
    private int _slot = -1;
    private MotorOutput _output;
    public string? Warning { get; private set; }
    public HapticDevice(Func<uint, MotorOutput, uint>? send = null) => _send = send ?? SendNative;
    public void Apply(int slot, MotorOutput output)
    {
        if (_slot != slot)
        {
            if (_slot >= 0 && _output != default) _send((uint)_slot, default);
            _slot = slot; _output = default; Warning = null;
        }
        if (slot < 0 || output == _output) return;
        if (_send((uint)slot, output) == 0) { _output = output; Warning = null; }
        else Warning = "Controller vibration is unavailable."; // Retain last state so failed stops are retried.
    }
    private static uint SendNative(uint slot, MotorOutput output)
    {
        var vibration = new NativeMethods.Vibration { Left = output.Left, Right = output.Right };
        return NativeMethods.SetVibration(slot, ref vibration);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControllerMouse.Core;
using Windows.Gaming.Input;

namespace ControllerMouse.App;

internal sealed record ControllerInventory(ControllerIdentity[] Devices, int[] Slots, string? Warning);
/// <summary>Model discovery stays outside the input loop; it never controls input or vibration.</summary>
internal sealed class ControllerIdentityObserver : IDisposable
{
    private readonly Func<int[]>? _readSlots;
    internal ControllerIdentityObserver(Func<int[]>? readSlots = null) => _readSlots = readSlots;
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;
    private readonly TaskCompletionSource<ControllerInventory> _firstScan = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<ControllerInventory> FirstScan => _firstScan.Task;
    private ControllerInventory _snapshot = new([], [], null);
    internal ControllerInventory Snapshot => Volatile.Read(ref _snapshot);
    internal void Start() => _worker ??= Task.Run(async () =>
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            do
            {
                try
                {
                    var devices = Windows.Gaming.Input.Gamepad.Gamepads.ToArray().Select(p => RawGameController.FromGameController(p))
                        .Where(p => p != null).Select(p => ControllerIdentity.Identify(p.HardwareVendorId, p.HardwareProductId, p.DisplayName)).ToArray();
                    var slots = _readSlots?.Invoke() ?? Enumerable.Range(0, 4).Where(i => NativeMethods.GetState((uint)i, out _) == 0).ToArray();
                    Volatile.Write(ref _snapshot, new(devices, slots, null));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Volatile.Write(ref _snapshot, new([], [], "Windows did not provide a controller model. Choose your controller type if needed."));
                }
                _firstScan.TrySetResult(Snapshot);
            } while (await timer.WaitForNextTickAsync(_cancel.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { }
    });
    public void Dispose() { _cancel.Cancel(); _worker?.GetAwaiter().GetResult(); _cancel.Dispose(); }
}

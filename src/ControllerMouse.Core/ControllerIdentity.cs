namespace ControllerMouse.Core;

public enum ControllerKind { Unknown, Standard, Adaptive }
public enum ControllerPreference { Automatic, Standard, Adaptive }
public sealed record ControllerIdentity(ControllerKind Kind, string Name, ushort Vendor, ushort Product)
{
    // Known Microsoft USB/Bluetooth/BLE IDs, also recorded in SDL's controller database.
    public static ControllerIdentity Identify(ushort vendor, ushort product, string? name)
    {
        var kind = vendor == 0x045e && product is 0x0b0a or 0x0b0c or 0x0b21 ? ControllerKind.Adaptive
            : vendor == 0x045e && product is 0x028e or 0x028f or 0x02d1 or 0x02dd or 0x02e0 or 0x02ea or 0x02fd or 0x02ff or 0x0b00 or 0x0b05 or 0x0b12 or 0x0b13 or 0x0b20 ? ControllerKind.Standard
            : ControllerKind.Unknown;
        return new(kind, kind == ControllerKind.Adaptive ? "Xbox Adaptive Controller" :
            string.IsNullOrWhiteSpace(name) ? "Xbox-compatible controller" : name, vendor, product);
    }
    public static ControllerKind Resolve(ControllerPreference preference, IReadOnlyList<ControllerIdentity> candidates,
        IReadOnlyList<int> slots, int activeSlot) => preference switch
        {
            ControllerPreference.Adaptive => ControllerKind.Adaptive,
            ControllerPreference.Standard => ControllerKind.Standard,
            _ => candidates.Count == 1 && slots.Count == 1 && slots[0] == activeSlot ? candidates[0].Kind : ControllerKind.Unknown
        };
}

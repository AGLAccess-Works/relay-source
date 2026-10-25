using System.Collections.ObjectModel;

namespace ControllerMouse.Core;

/// <summary>A connected XInput slot, with model identity only when association is unambiguous.</summary>
public sealed record ControllerDevice(int Slot, string Name, ControllerKind Kind, bool Selected);

/// <summary>Pure selection policy; hardware reads and input cleanup remain in the existing service.</summary>
public sealed class ControllerSelection
{
    private ReadOnlyCollection<int> _slots = Array.AsReadOnly(Array.Empty<int>());
    public IReadOnlyList<int> Slots => _slots;
    public int SelectedSlot { get; private set; } = -1;

    public bool UpdateConnectedSlots(IEnumerable<int> slots)
    {
        int previous = SelectedSlot;
        _slots = Array.AsReadOnly(slots.Where(slot => slot is >= 0 and < 4).Distinct().Order().ToArray());
        if (!_slots.Contains(SelectedSlot)) SelectedSlot = _slots.Count == 0 ? -1 : _slots[0];
        return previous != SelectedSlot;
    }

    public bool Select(int slot)
    {
        if (!_slots.Contains(slot)) return false;
        SelectedSlot = slot; return true;
    }

    public IReadOnlyList<ControllerDevice> Describe(ControllerPreference preference,
        IReadOnlyList<ControllerIdentity> identities, IReadOnlyList<int> identitySlots)
    {
        // Metadata has its own cadence. Never associate a stale or ambiguous inventory with a slot.
        bool unique = _slots.Count == 1 && identities.Count == 1 && identitySlots.Count == 1 && identitySlots[0] == _slots[0];
        return Array.AsReadOnly(_slots.Select(slot =>
        {
            bool selected = slot == SelectedSlot;
            var selectedPreference = selected ? preference : ControllerPreference.Automatic;
            var kind = selectedPreference switch
            {
                ControllerPreference.Adaptive => ControllerKind.Adaptive,
                ControllerPreference.Standard => ControllerKind.Standard,
                _ => unique ? identities[0].Kind : ControllerKind.Unknown
            };
            string model = kind switch
            {
                ControllerKind.Adaptive => "Xbox Adaptive Controller",
                ControllerKind.Standard => "Xbox controller",
                _ => "Xbox-compatible controller"
            };
            string chosen = selected && preference != ControllerPreference.Automatic ? " (chosen type)" : "";
            return new ControllerDevice(slot, $"Controller {slot + 1} · {model}{chosen}", kind, selected);
        }).ToArray());
    }
}

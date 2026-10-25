namespace ControllerMouse.Core;

/// <summary>Latched UI modifiers; no operating-system keys remain held between clicks.</summary>
public sealed class KeyboardModifiers
{
    private readonly HashSet<ushort> _selected = [];
    private static readonly ushort[] Order = [0x11, 0x12, 0x10, 0x5B];
    public bool Selected(ushort key) => _selected.Contains(key);
    public void Toggle(ushort key)
    {
        if (!Order.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key));
        if (!_selected.Remove(key)) _selected.Add(key);
    }
    public ushort[] Consume(ushort key)
    {
        if (key is < 8 or > 254 || Order.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key));
        var result = Order.Where(_selected.Contains).Append(key).ToArray();
        Clear();
        return result;
    }
    public void Clear() => _selected.Clear();
}

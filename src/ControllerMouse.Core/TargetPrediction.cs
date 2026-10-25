using System.Text.Json;

namespace ControllerMouse.Core;

public readonly record struct PointD(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(PointD a, double s) => new(a.X * s, a.Y * s);
}

public sealed record TargetCandidate(double Left, double Top, double Width, double Height, PointD Aim, string Kind)
{
    public bool Contains(PointD p) => p.X >= Left && p.X <= Left + Width && p.Y >= Top && p.Y <= Top + Height;
    public bool Valid => double.IsFinite(Left) && double.IsFinite(Top) && double.IsFinite(Width) &&
        double.IsFinite(Height) && double.IsFinite(Aim.X) && double.IsFinite(Aim.Y) &&
        Width is >= 10 and <= 600 && Height is >= 10 and <= 350 && Contains(Aim);
}

public static class TargetPrediction
{
    public static TargetCandidate? Select(PointD cursor, PointD velocity, IEnumerable<TargetCandidate> candidates,
        IReadOnlyDictionary<string, int>? preferences = null)
    {
        double speed = velocity.Length;
        if (!double.IsFinite(speed) || speed < 15 || speed > 1800) return null;
        PointD direction = velocity * (1 / speed);
        TargetCandidate? best = null;
        double bestScore = double.MaxValue;
        int total = preferences?.Values.Sum() ?? 0;
        foreach (var target in candidates)
        {
            if (!target.Valid) continue;
            var offset = target.Aim - cursor;
            double distance = offset.Length;
            if (distance > 200) continue;
            double along = offset.X * direction.X + offset.Y * direction.Y;
            double across = Math.Abs(offset.X * direction.Y - offset.Y * direction.X);
            bool inside = target.Contains(cursor);
            if (!inside && (along < 0 || across > 35 + along * .3)) continue;
            double score = inside ? distance * .2 : distance + across * 2.5;
            if (total > 0 && preferences!.TryGetValue(target.Kind, out int count)) score *= 1 - .12 * count / total;
            if (score < bestScore) { bestScore = score; best = target; }
        }
        return best;
    }

    // Bends existing motion only: no initiated movement, clicks, reversal, or assistance during drag.
    public static PointD Assist(PointD cursor, PointD delta, TargetCandidate? target, double strength, bool dragging)
    {
        double moved = delta.Length;
        if (dragging || target is not { Valid: true } || moved == 0 || !double.IsFinite(moved)) return delta;
        var offset = target.Aim - cursor;
        double distance = offset.Length;
        if (distance > 200 || distance < 1) return delta;
        if (offset.X * delta.X + offset.Y * delta.Y <= 0) return delta;
        double amount = Math.Clamp(double.IsFinite(strength) ? strength : 0, 0, 1);
        if (amount == 0) return delta;
        if (target.Contains(cursor)) return delta * (1 - .45 * amount);
        double blend = .3 * amount * (1 - Math.Min(1, distance / 200));
        var blended = delta * (1 - blend) + offset * (moved / distance * blend);
        double length = blended.Length;
        return length > 0 ? blended * (Math.Min(length, Math.Min(moved, distance)) / length) : delta;
    }
}

public sealed class TargetMemory
{
    private static readonly HashSet<string> Kinds = ["Button", "Hyperlink", "CheckBox", "RadioButton", "TabItem", "ComboBox", "Edit", "MenuItem", "ListItem", "TreeItem"];
    private readonly Dictionary<string, int> _counts = [];
    public IReadOnlyDictionary<string, int> Counts => _counts;
    public int Samples => _counts.Values.Sum();
    public void Record(string kind)
    {
        if (!Kinds.Contains(kind)) return;
        if (Samples >= 1000) foreach (string key in _counts.Keys.ToArray()) _counts[key] /= 2;
        _counts[kind] = _counts.GetValueOrDefault(kind) + 1;
    }
    public void Clear() => _counts.Clear();
    public string Serialize() => JsonSerializer.Serialize(_counts);
    public static TargetMemory Deserialize(string json)
    {
        var memory = new TargetMemory();
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (data != null) foreach (var (kind, count) in data)
                if (Kinds.Contains(kind) && count > 0) memory._counts[kind] = Math.Min(count, 1000);
        }
        catch (JsonException) { }
        return memory;
    }
}

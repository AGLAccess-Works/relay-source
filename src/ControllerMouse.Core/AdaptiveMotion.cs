using System.Text.Json;

namespace ControllerMouse.Core;

public enum LearningMode { Off, Learn, Frozen }
public sealed record MotionModel(double Seconds = 0, double Variation = 0, double Smoothing = 0)
{
    public bool Valid => double.IsFinite(Seconds) && Seconds is >= 0 and <= 86400 &&
        double.IsFinite(Variation) && Variation is >= 0 and <= 1 && double.IsFinite(Smoothing) && Smoothing is >= 0 and <= .04;
}

// Learns variability, not intent: never interprets a click or edits the user's work.
public sealed class AdaptiveMotion
{
    public MotionModel Model { get; private set; } = new();
    private double _trendX, _trendY, _strokeSeconds, _windowSeconds, _energy, _pending;
    public double ObservedSeconds => Model.Seconds + _windowSeconds;
    public double PendingSmoothing => _pending;
    public string Activity { get; private set; } = "Waiting for movement";
    public void Load(MotionModel model) { Model = model.Valid ? model : new(); _pending = Model.Smoothing; _windowSeconds = _energy = 0; ResetStroke(); }
    private void ResetGesture() { _trendX = _trendY = _strokeSeconds = 0; }
    public void ResetStroke() { ResetGesture(); Activity = "Waiting for movement"; }
    public void Forget() => Load(new());
    public void Observe(double x, double y, double dt, LearningMode mode, bool excluded)
    {
        if (mode != LearningMode.Learn || excluded || !double.IsFinite(dt) || dt <= 0 || dt > .05 ||
            !double.IsFinite(x) || !double.IsFinite(y)) { ResetGesture(); Activity = excluded ? "Paused while buttons or scrolling are in use" : "Not collecting"; return; }
        double magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude == 0)
        {
            // Commit only between gestures; ordinary resting input contributes no samples.
            Model = Model with { Smoothing = _pending }; ResetGesture(); Activity = "Waiting for movement"; return;
        }
        if (magnitude is < .015 or > .85) { ResetGesture(); Activity = "Outside the fine-movement range"; return; }
        if (_strokeSeconds == 0) { _trendX = x; _trendY = y; }
        double blend = 1 - Math.Exp(-dt / .12);
        _trendX += (x - _trendX) * blend; _trendY += (y - _trendY) * blend;
        double residual = Math.Sqrt(Math.Pow(x - _trendX, 2) + Math.Pow(y - _trendY, 2));
        _strokeSeconds += dt;
        Activity = "Collecting fine movement";
        if (_strokeSeconds < .02) return;
        _energy += Math.Clamp(residual / Math.Max(.1, magnitude), 0, 1) * dt;
        _windowSeconds += dt;
        if (_windowSeconds < 1) return;
        double evidence = _energy / _windowSeconds;
        double seconds = Math.Min(86400, Model.Seconds + _windowSeconds);
        double weight = Math.Min(1, _windowSeconds / Math.Min(60, seconds));
        double variation = Model.Variation + (evidence - Model.Variation) * weight;
        Model = Model with { Seconds = seconds, Variation = variation };
        if (seconds >= 5)
        {
            double target = Math.Clamp((variation - .04) * .16, 0, .04);
            // Small, bounded steps after warm-up; commit between gestures.
            _pending = Math.Clamp(_pending + Math.Clamp(target - _pending, -.004, .004), 0, .04);
        }
        _windowSeconds = _energy = 0;
    }
    public (double Smoothing, double Speed) Adjustment(LearningMode mode, double magnitude, bool excluded)
    {
        if (mode == LearningMode.Off || excluded || magnitude <= 0) return (0, 1);
        double fine = Math.Clamp((.9 - magnitude) / .6, 0, 1);
        double smoothing = Model.Smoothing * fine;
        return (smoothing, 1 - .15 * smoothing / .04);
    }
}

public sealed class MotionLearningStore(string? path)
{
    private Dictionary<string, MotionModel> _profiles = new();
    public string? Warning { get; private set; }
    public void Load()
    {
        if (path == null || !File.Exists(path)) return;
        try
        {
            if (new FileInfo(path).Length > 100_000) throw new JsonException();
            var data = JsonSerializer.Deserialize<Dictionary<string, MotionModel>>(File.ReadAllText(path));
            if (data == null || data.Count > 64 || data.Any(p => p.Key.Length > 100 || p.Value == null || !p.Value.Valid)) throw new JsonException();
            _profiles = data;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { Warning = "Motion learning could not be loaded. Starting fresh."; }
    }
    public MotionModel Get(string id) => _profiles.GetValueOrDefault(id) ?? new();
    public void Put(string id, MotionModel model)
    {
        if (model.Valid && id.Length <= 100 && (_profiles.ContainsKey(id) || _profiles.Count < 64)) _profiles[id] = model;
    }
    public void Retain(IEnumerable<string> ids)
    {
        var retained = ids.ToHashSet();
        foreach (var id in _profiles.Keys.Where(id => !retained.Contains(id)).ToArray()) _profiles.Remove(id);
    }
    public void Save()
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(_profiles));
            File.Move(path + ".tmp", path, true); Warning = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Warning = "Motion learning could not be saved; current learning may be lost on exit."; }
    }
}

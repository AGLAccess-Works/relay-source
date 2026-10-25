using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal sealed record PredictionSnapshot(TargetCandidate Candidate, IntPtr Window, double Time, int Generation);
internal sealed record TargetRequest(PointD Cursor, PointD Velocity, IntPtr Window, double Time, int Generation);

/// <summary>UI Automation runs on one background MTA thread, never the input or WPF thread.</summary>
internal sealed class WindowsTargetAssistant : IDisposable
{
    private readonly object _memoryGate = new();
    private readonly string? _memoryPath;
    private TargetMemory _memory = new();
    private bool _dirty;
    private int _memoryVersion;
    private readonly object _flushGate = new();
    private volatile bool _stopping;
    private Thread? _thread;
    private Settings _settings = new();
    private TargetRequest? _request;
    private PredictionSnapshot? _prediction;
    private double _dt = .008, _fractionX, _fractionY;
    private PointD _velocity;
    private volatile bool _active, _moving, _dragging;
    private int _generation;
    private string? _warning;

    internal WindowsTargetAssistant(string? memoryPath)
    {
        _memoryPath = memoryPath;
        try
        {
            if (memoryPath != null && File.Exists(memoryPath) && new FileInfo(memoryPath).Length < 32768)
                _memory = TargetMemory.Deserialize(File.ReadAllText(memoryPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _warning = "Local target memory could not be read."; }
    }
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    internal int LearnedSelections { get { lock (_memoryGate) return _memory.Samples; } }
    internal string? Warning => Volatile.Read(ref _warning);
    internal PredictionSnapshot? Prediction
    {
        get
        {
            var p = Volatile.Read(ref _prediction);
            return _active && _moving && !_dragging && p != null && p.Generation == Volatile.Read(ref _generation) && Now - p.Time < .35 && NativeMethods.GetForegroundWindow() == p.Window ? p : null;
        }
    }
    internal void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(ScanLoop) { IsBackground = true, Name = "Controller target detection" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }
    internal void Configure(Settings settings)
    {
        Volatile.Write(ref _settings, settings.Validate());
        if (!settings.TargetAssist) ClearMotion();
    }
    internal void BeginFrame(double dt, bool active, bool moving, bool dragging)
    {
        _dt = Math.Clamp(dt, .001, .05); _active = active; _moving = moving; _dragging = dragging;
        if (!active) ClearMotion();
        else if (!moving || dragging) { Volatile.Write(ref _request, null); _velocity = default; _fractionX = _fractionY = 0; }
    }
    internal void ClearMotion()
    {
        Interlocked.Increment(ref _generation);
        Volatile.Write(ref _request, null); Volatile.Write(ref _prediction, null);
        _velocity = default; _fractionX = _fractionY = 0;
    }
    internal (int X, int Y) Adjust(int x, int y)
    {
        var settings = Volatile.Read(ref _settings);
        if (!settings.TargetAssist || !_active || !_moving || _dragging || !NativeMethods.GetPhysicalCursorPos(out var cursor)) return (x, y);
        var delta = new PointD(x, y);
        double alpha = 1 - Math.Exp(-_dt / .05);
        _velocity = _velocity * (1 - alpha) + delta * (alpha / _dt);
        var point = new PointD(cursor.X, cursor.Y);
        Volatile.Write(ref _request, new TargetRequest(point, _velocity, NativeMethods.GetForegroundWindow(), Now, Volatile.Read(ref _generation)));
        var prediction = Prediction;
        var adjusted = TargetPrediction.Assist(point, delta, prediction?.Candidate, settings.TargetAssistStrength, false);
        if (adjusted == delta) { _fractionX = _fractionY = 0; return (x, y); }
        _fractionX += adjusted.X; _fractionY += adjusted.Y;
        int mx = (int)_fractionX, my = (int)_fractionY;
        _fractionX -= mx; _fractionY -= my;
        return (mx, my);
    }
    internal void RecordClick()
    {
        var settings = Volatile.Read(ref _settings);
        if (!settings.TargetAssist || !settings.RememberTargets) return;
        var prediction = Volatile.Read(ref _prediction);
        if (prediction == null || prediction.Generation != Volatile.Read(ref _generation) || Now - prediction.Time > 2 || NativeMethods.GetForegroundWindow() != prediction.Window || !NativeMethods.GetPhysicalCursorPos(out var point) ||
            !prediction.Candidate.Contains(new PointD(point.X, point.Y))) return;
        lock (_memoryGate)
        {
            var current = Volatile.Read(ref _settings);
            if (prediction.Generation != Volatile.Read(ref _generation) || !current.TargetAssist || !current.RememberTargets) return;
            _memory.Record(prediction.Candidate.Kind); _dirty = true; _memoryVersion++;
        }
    }
    internal void Forget()
    {
        ClearMotion();
        lock (_memoryGate) { _memory.Clear(); _dirty = true; _memoryVersion++; }
        Flush();
    }

    private void ScanLoop()
    {
        double lastSave = 0;
        while (!_stopping)
        {
            try
            {
                if (Now - lastSave > 2) { Flush(); lastSave = Now; }
                var settings = Volatile.Read(ref _settings);
                var request = Volatile.Read(ref _request);
                if (settings.TargetAssist && request != null && Now - request.Time < .2 && request.Window != IntPtr.Zero)
                {
                    var candidates = Detect(request);
                    Dictionary<string, int>? counts = null;
                    if (settings.RememberTargets) lock (_memoryGate) counts = new Dictionary<string, int>(_memory.Counts);
                    var current = Volatile.Read(ref _request);
                    // Discard results if input stopped, the window changed, or detection was slow.
                    if (!_stopping && current != null && current.Generation == request.Generation &&
                        request.Generation == Volatile.Read(ref _generation) && current.Window == request.Window &&
                        NativeMethods.GetForegroundWindow() == request.Window && Now - request.Time < .3 &&
                        (current.Cursor - request.Cursor).Length < 80)
                    {
                        var target = TargetPrediction.Select(current.Cursor, current.Velocity, candidates, counts);
                        Volatile.Write(ref _prediction, target == null ? null : new PredictionSnapshot(target, current.Window, Now, request.Generation));
                    }
                }
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException)
            { Volatile.Write(ref _prediction, null); }
            catch (Exception ex) { Log.Write(ex); Volatile.Write(ref _warning, "Target detection is unavailable. Pointer control still works."); }
            Thread.Sleep(100);
        }
    }

    internal List<TargetCandidate> ProbeTargets(PointD cursor, PointD velocity, IntPtr window)
    {
        // Warm up UIA before starting the normal bounded scan.
        _ = AutomationElement.FromHandle(window).Current.BoundingRectangle;
        var result = Detect(new TargetRequest(cursor, velocity, window, Now, Volatile.Read(ref _generation)));
        if (result.Count == 0)
        {
            var details = new System.Text.StringBuilder();
            var element = AutomationElement.FromPoint(new Point(cursor.X, cursor.Y));
            for (int depth = 0; element != null && depth < 4; depth++, element = TreeWalker.ControlViewWalker.GetParent(element))
            {
                var info = element.Current; bool clickable = element.TryGetClickablePoint(out var aim);
                details.AppendLine($"{info.ControlType.ProgrammaticName} rect={info.BoundingRectangle} enabled={info.IsEnabled} offscreen={info.IsOffscreen} password={info.IsPassword} clickable={clickable} aim={aim}");
            }
            throw new InvalidOperationException("UIA fixture inspection: " + details);
        }
        return result;
    }

    private List<TargetCandidate> Detect(TargetRequest request)
    {
        var candidates = new List<TargetCandidate>();
        double speed = request.Velocity.Length;
        if (speed < 15 || speed > 1800) return candidates;
        var dir = request.Velocity * (1 / speed);
        var side = new PointD(-dir.Y, dir.X);
        foreach (double distance in new[] { 0d, 45, 100, 170 })
            foreach (double cross in new[] { 0d, -25, 25 })
            {
                if (_stopping || Now - request.Time > .25) return candidates;
                var sample = request.Cursor + dir * distance + side * cross;
                var handle = NativeMethods.WindowFromPoint(new NativeMethods.Point { X = (int)sample.X, Y = (int)sample.Y });
                if (NativeMethods.GetAncestor(handle, 2) != request.Window) continue;
                var element = AutomationElement.FromPoint(new Point(sample.X, sample.Y));
                for (int depth = 0; element != null && depth < 4; depth++, element = TreeWalker.ControlViewWalker.GetParent(element))
                {
                    var info = element.Current;
                    if (!info.IsEnabled || info.IsOffscreen || info.IsPassword) break;
                    string kind = info.ControlType.ProgrammaticName.Replace("ControlType.", "", StringComparison.Ordinal);
                    if (kind is not ("Button" or "Hyperlink" or "CheckBox" or "RadioButton" or "TabItem" or "ComboBox" or "Edit" or "MenuItem" or "ListItem" or "TreeItem")) continue;
                    var rect = info.BoundingRectangle;
                    if (rect.IsEmpty || !element.TryGetClickablePoint(out var aim)) break;
                    var candidate = new TargetCandidate(rect.Left, rect.Top, rect.Width, rect.Height, new PointD(aim.X, aim.Y), kind);
                    if (candidate.Valid && !candidates.Contains(candidate)) candidates.Add(candidate);
                    break;
                }
            }
        return candidates;
    }
    private void Flush()
    {
        lock (_flushGate)
        {
            string json; int version;
            lock (_memoryGate)
            {
                if (!_dirty || _memoryPath == null) return;
                json = _memory.Serialize(); version = _memoryVersion;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_memoryPath)!);
                File.WriteAllText(_memoryPath + ".tmp", json);
                File.Move(_memoryPath + ".tmp", _memoryPath, true);
                lock (_memoryGate) { if (_memoryVersion == version) _dirty = false; }
                Volatile.Write(ref _warning, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Volatile.Write(ref _warning, "Target preferences could not be saved on this PC."); }
        }
    }
    public void Dispose()
    {
        _stopping = true; ClearMotion();
        // A broken third-party UIA provider must never hold up quitting or releasing input.
        _thread?.Join(150); Flush();
    }
}

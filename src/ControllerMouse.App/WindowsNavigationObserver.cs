using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using ControllerMouse.Core;

namespace ControllerMouse.App;

// UI Automation providers can block: never call them on the controller or UI thread.
internal sealed class WindowsNavigationObserver : IDisposable
{
    internal sealed record Request(long Generation, IntPtr Window, int X, int Y, double Time,
        bool Hover, bool Boundary, int Vertical, int Horizontal);
    internal sealed record Feedback(HapticCue Cue, Request Source);
    private Request? _request;
    private Feedback? _feedback;
    private readonly Thread _thread;
    private volatile bool _stopping;
    private long _generation;
    public WindowsNavigationObserver()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Navigation feedback" };
        _thread.SetApartmentState(ApartmentState.MTA);
    }
    public void Start() => _thread.Start();
    public void Reset() { Interlocked.Increment(ref _generation); Volatile.Write(ref _request, null); Interlocked.Exchange(ref _feedback, null); }
    public void Update(bool hover, bool boundary, int vertical, int horizontal, double now)
    {
        if ((!hover && !boundary) || !NativeMethods.GetPhysicalCursorPos(out var point)) { Reset(); return; }
        Volatile.Write(ref _request, new(Interlocked.Read(ref _generation), NativeMethods.GetForegroundWindow(),
            point.X, point.Y, now, hover, boundary, vertical, horizontal));
    }
    public HapticCue? Take(double now)
    {
        var feedback = Interlocked.Exchange(ref _feedback, null);
        var current = Volatile.Read(ref _request);
        if (feedback == null || current == null || current.Hover != feedback.Source.Hover || current.Boundary != feedback.Source.Boundary || feedback.Source.Generation != Interlocked.Read(ref _generation) ||
            now - feedback.Source.Time > .3 || feedback.Source.Window != NativeMethods.GetForegroundWindow() ||
            !NativeMethods.GetPhysicalCursorPos(out var point) ||
            Math.Abs(point.X - feedback.Source.X) > 6 || Math.Abs(point.Y - feedback.Source.Y) > 6) return null;
        return feedback.Cue;
    }
    internal static (string? Target, string? Region, double Vertical, double Horizontal) Probe(Request request)
    {
        if (request.Window == IntPtr.Zero ||
            NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(new NativeMethods.Point { X = request.X, Y = request.Y }), 2) != request.Window)
            return (null, null, -1, -1);
        var point = new Point(request.X, request.Y);
        var element = AutomationElement.FromPoint(point);
        string? target = null, region = null;
        double vertical = -1, horizontal = -1;
        var budget = Stopwatch.StartNew();
        for (int depth = 0; element != null && depth < 8 && budget.ElapsedMilliseconds < 200; depth++)
        {
            var current = element.Current;
            if (current.IsPassword) return (null, null, -1, -1);
            if (current.IsEnabled && !current.IsOffscreen && current.BoundingRectangle.Contains(point))
            {
                if (request.Hover && target == null && (current.ControlType == ControlType.Button ||
                    current.ControlType == ControlType.Hyperlink || current.ControlType == ControlType.CheckBox ||
                    current.ControlType == ControlType.RadioButton || current.ControlType == ControlType.TabItem))
                    target = string.Join(".", element.GetRuntimeId()) + current.BoundingRectangle.ToString();
                if (request.Boundary && region == null && element.TryGetCurrentPattern(ScrollPattern.Pattern, out var pattern))
                {
                    var scroll = ((ScrollPattern)pattern).Current;
                    if (scroll.VerticallyScrollable || scroll.HorizontallyScrollable)
                    {
                        region = string.Join(".", element.GetRuntimeId());
                        vertical = scroll.VerticallyScrollable ? scroll.VerticalScrollPercent : -1;
                        horizontal = scroll.HorizontallyScrollable ? scroll.HorizontalScrollPercent : -1;
                    }
                }
            }
            if ((!request.Hover || target != null) && (!request.Boundary || region != null)) break;
            element = TreeWalker.ControlViewWalker.GetParent(element);
        }
        return (target, region, vertical, horizontal);
    }
    private void Run()
    {
        var tracker = new NavigationCueTracker();
        long generation = -1;
        IntPtr window = IntPtr.Zero;
        while (!_stopping)
        {
            Thread.Sleep(80);
            var request = Volatile.Read(ref _request);
            if (request == null) { tracker.Reset(); continue; }
            if (request.Generation != generation || request.Window != window)
            { tracker.Reset(); generation = request.Generation; window = request.Window; }
            try
            {
                var snapshot = Probe(request);
                var latest = Volatile.Read(ref _request);
                double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                if (latest == null || latest.Generation != request.Generation || latest.Window != request.Window ||
                    now - request.Time > .3 || latest.Hover != request.Hover || latest.Boundary != request.Boundary)
                { tracker.Reset(); continue; }
                bool target = tracker.Hover(snapshot.Target, now);
                bool boundary = tracker.Boundary(snapshot.Region, snapshot.Vertical, snapshot.Horizontal, request.Vertical, request.Horizontal);
                if (boundary || target) Interlocked.Exchange(ref _feedback, new(boundary ? HapticCue.Boundary : HapticCue.Target, request));
            }
            catch (Exception) { tracker.Reset(); } // Inaccessible or disappearing providers are optional feedback.
        }
    }
    public void Dispose() { _stopping = true; Reset(); if (_thread.IsAlive) _thread.Join(150); }
}

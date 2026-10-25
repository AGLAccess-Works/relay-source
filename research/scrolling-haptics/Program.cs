using ControllerMouse.Core;
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.WriteLine("Current 1.2 core; synthetic inputs; 125 Hz; defaults; no Windows input or haptics.");
Console.WriteLine("deflection_percent,first_event_ms,event_count_60s,distinct_deltas");
foreach (int percentage in new[] { 20, 25, 50, 75, 100 })
{
    var sink = new TraceSink();
    var engine = new InputProcessor(sink);
    engine.Process(default(Gamepad), .008);
    for (int i = 1; i <= 7500; i++)
    {
        sink.Time = i * .008;
        engine.Process(new Gamepad(Buttons.None, 0, 0, 0, 0, 0, (short)(32767 * percentage / 100)), .008);
    }
    Console.WriteLine($"{percentage},{sink.Events.FirstOrDefault().Time * 1000:0},{sink.Events.Count},{string.Join("|", sink.Events.Select(e => e.Delta).Distinct())}");
}
var nudges = new TraceSink(); var nudgeEngine = new InputProcessor(nudges);
nudgeEngine.Process(default(Gamepad), .008);
for (int j = 0; j < 10; j++)
{
    for (int i = 0; i < 25; i++) nudgeEngine.Process(new Gamepad(Buttons.None, 0, 0, 0, 0, 0, 16383), .008);
    nudgeEngine.Process(default(Gamepad), .008);
}
Console.WriteLine($"Ten separate 200 ms nudges at 50%: {nudges.Events.Count} wheel events.");

foreach (Buttons modifier in new[] { Buttons.None, Buttons.LeftShoulder, Buttons.RightShoulder })
{
    var sink = new TraceSink(); var engine = new InputProcessor(sink); engine.Process(default(Gamepad), .008);
    for (int i = 0; i < 125; i++) engine.Process(new Gamepad(modifier, 0, 0, 0, 0, 0, 32767), .008);
    Console.WriteLine($"One second full right stick, {modifier}: {sink.Events.Sum(e => e.Delta)} wheel units.");
}
sealed class TraceSink : IInputSink
{
    public double Time;
    public List<(double Time, int Delta)> Events = [];
    public void Move(int x, int y) { }
    public void Scroll(int vertical, int horizontal) { if (vertical != 0) Events.Add((Time, vertical)); }
    public void SetMouseButton(MouseButton button, bool down) { }
    public void Invoke(Shortcut shortcut) { }
}

using ControllerMouse.Core;

internal static class ControllerSelectionTests
{
    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run(Action<string, Action> test)
    {
        test("Connected controller inventory is ordered, distinct and restricted to XInput slots", () =>
        {
            var selection = new ControllerSelection();
            Check(selection.UpdateConnectedSlots([3, 1, 3, -1, 4, 20]));
            Check(selection.Slots.SequenceEqual([1, 3]) && selection.SelectedSlot == 1);
            var devices = selection.Describe(ControllerPreference.Automatic, [], []);
            Check(devices.Count == 2 && devices[0].Slot == 1 && devices[0].Selected && !devices[1].Selected);
            Check(devices.All(d => d.Kind == ControllerKind.Unknown && d.Name.Contains("Xbox-compatible")));
        });
        test("Manual selection stays selected while present and disconnect safely falls back", () =>
        {
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([1, 3]);
            Check(selection.Select(3)); Check(!selection.UpdateConnectedSlots([0, 1, 3]) && selection.SelectedSlot == 3);
            Check(selection.UpdateConnectedSlots([0, 1]) && selection.SelectedSlot == 0);
            Check(!selection.UpdateConnectedSlots([0, 1, 3]) && selection.SelectedSlot == 0);
            Check(selection.UpdateConnectedSlots([]) && selection.SelectedSlot == -1);
            Check(selection.Describe(ControllerPreference.Adaptive, [], []).Count == 0);
        });
        test("Unavailable controller selection cannot change the active controller", () =>
        {
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([2]);
            foreach (int slot in new[] { -1, 0, 1, 3, 4, int.MaxValue }) Check(!selection.Select(slot));
            Check(selection.SelectedSlot == 2 && selection.Select(2));
        });
        test("Multiple controllers never receive unrelated or ambiguous model metadata", () =>
        {
            var adaptive = ControllerIdentity.Identify(0x045e, 0x0b0a, "Adaptive");
            var standard = ControllerIdentity.Identify(0x045e, 0x0b12, "Xbox");
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([0, 2]);
            var devices = selection.Describe(ControllerPreference.Automatic, [adaptive, standard], [0, 2]);
            Check(devices.All(d => d.Kind == ControllerKind.Unknown && d.Name.Contains("Xbox-compatible")));
            Check(selection.Describe(ControllerPreference.Automatic, [adaptive], [0]).All(d => d.Kind == ControllerKind.Unknown));
        });
        test("Single controller model resolves only with a current matching unique inventory", () =>
        {
            var adaptive = ControllerIdentity.Identify(0x045e, 0x0b0a, "Adaptive");
            var standard = ControllerIdentity.Identify(0x045e, 0x0b12, "Xbox");
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([2]);
            Check(selection.Describe(ControllerPreference.Automatic, [adaptive], [2])[0].Kind == ControllerKind.Adaptive);
            Check(selection.Describe(ControllerPreference.Automatic, [standard], [2])[0].Kind == ControllerKind.Standard);
            Check(selection.Describe(ControllerPreference.Automatic, [adaptive], [0])[0].Kind == ControllerKind.Unknown);
            Check(selection.Describe(ControllerPreference.Automatic, [adaptive, standard], [2])[0].Kind == ControllerKind.Unknown);
        });
        test("Manually chosen controller type applies only to the selected slot", () =>
        {
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([0, 2]); selection.Select(2);
            var devices = selection.Describe(ControllerPreference.Adaptive, [], []);
            Check(devices[0].Kind == ControllerKind.Unknown && devices[1].Kind == ControllerKind.Adaptive);
            Check(!devices[0].Name.Contains("chosen") && devices[1].Name.Contains("chosen type"));
            selection.Select(0); var next = selection.Describe(ControllerPreference.Standard, [], []);
            Check(next[0].Kind == ControllerKind.Standard && next[1].Kind == ControllerKind.Unknown);
            Check(devices[1].Selected && !devices[0].Selected, "An earlier device snapshot was mutated.");
        });
        test("Controller inventory snapshots cannot be altered by callers", () =>
        {
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([0, 1]);
            Check(((ICollection<int>)selection.Slots).IsReadOnly);
            var devices = selection.Describe(ControllerPreference.Automatic, [], []);
            Check(((ICollection<ControllerDevice>)devices).IsReadOnly);
            selection.UpdateConnectedSlots([3]);
            Check(devices.Count == 2 && devices[0].Slot == 0 && selection.SelectedSlot == 3);
        });
        test("Switching the central engine releases drag and prevents held new-controller replay", () =>
        {
            var selection = new ControllerSelection(); selection.UpdateConnectedSlots([0, 2]);
            var sink = new Recorder(); var engine = new InputProcessor(sink);
            engine.Process(default(Gamepad), .008);
            engine.Process(new(Buttons.A, 0, 0, 0, 0, 0, 0), .008);
            Check(sink.Events.Contains("Left:True"));
            Check(selection.Select(2)); engine.Process(null, 0);
            Check(!engine.Armed && sink.Events.Last() == "Left:False"); sink.Events.Clear();
            engine.Process(new(Buttons.A, 0, 0, 32767, 0, 0, 0), .008);
            Check(!engine.Armed && sink.Events.Count == 0 && sink.X == 0);
            engine.Process(default(Gamepad), .008); Check(engine.Armed && sink.Events.Count == 0);
            engine.Process(new(Buttons.A, 0, 0, 0, 0, 0, 0), .008);
            Check(sink.Events.SequenceEqual(["Left:True"]));
        });
    }
}

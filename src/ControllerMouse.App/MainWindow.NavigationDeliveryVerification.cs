using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunNavigationDeliveryChecks(string output)
    {
        int originalPage = Pages.SelectedIndex;
        var sink = new NavigationVerificationSink();
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { NarratorNavigation = true, Smoothing = 0, ClickHoldMilliseconds = 0 },
            RelayWindowActive = true
        };
        int delivered = 0, discarded = 0, focusChanges = 0;
        var report = new List<string>();
        engine.RelayNavigationRequested = command =>
        {
            long generation = engine.CaptureRelayNavigationGeneration(command);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!engine.IsRelayNavigationCurrent(command, generation)) { discarded++; return; }
                delivered++; NavigateController(command);
            }));
        };
        static Gamepad Pad(Buttons button = Buttons.None, short x = 0) => new(button, 0, 0, x, 0, 0, 0);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        KeyboardFocusChangedEventHandler changed = (_, e) => { if (e.NewFocus != null) focusChanges++; };
        void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Queued navigation delivery: " + detail);
        }
        void FocusSidebar(int page)
        {
            SelectPage(page);
            foreach (var radio in NavigationItems())
                if (radio.Tag?.ToString() == page.ToString()) radio.Focus();
            delivered = discarded = focusChanges = 0;
        }
        void ReleasedBeforeDispatch(Buttons direction)
        {
            engine.Process(Pad(), .008);
            engine.Process(Pad(direction), .008);
            for (int frame = 0; frame < 10; frame++) engine.Process(Pad(direction, 20000), .008);
            engine.Process(Pad(x: 20000), .008);
        }
        AddHandler(Keyboard.GotKeyboardFocusEvent, changed, true);
        try
        {
            engine.Process(Pad(), .008);
            FocusSidebar(0);
            ReleasedBeforeDispatch(Buttons.Down);
            Check(delivered == 0 && !engine.KeyboardNavigationActive && engine.Armed,
                "fixture did not queue the command until after the fresh stick return.");
            await Drain();
            Check(delivered == 1 && discarded == 0 && focusChanges == 1 &&
                Pages.SelectedIndex == 6 && CurrentNavigation().IsKeyboardFocused && sink.MoveCalls > 0,
                "released Down was dropped or doubled when fresh stick movement preceded UI dispatch.");
            report.Add("PASS Down starts at centred stick; strong movement begins while held; release returns pointer ownership before UI dispatch. Down still reaches Mapping with exactly one real focus event and recorded pointer movement.");

            FocusSidebar(0);
            foreach (var direction in new[] { Buttons.Down, Buttons.Down, Buttons.Down, Buttons.Up, Buttons.Up, Buttons.Down })
                ReleasedBeforeDispatch(direction);
            await Drain();
            Check(delivered == 6 && discarded == 0 && focusChanges == 6 && Pages.SelectedIndex == 9,
                "delayed rapid direction edges were lost after pointer ownership changes.");
            report.Add("PASS six rapid Down/Down/Down/Up/Up/Down edges, each released with fresh stick movement before UI dispatch: six delivered commands and six actual focus changes in order.");

            engine.Process(Pad(), .008);
            engine.Process(Pad(Buttons.Down, 20000), .008);
            engine.Process(Pad(x: 20000), .008);
            await Drain();
            SelectPage(7); LightThemeCheck.Focus();
            bool originalTheme = LightThemeCheck.IsChecked == true;
            delivered = discarded = focusChanges = 0;
            engine.Process(Pad(Buttons.A, 20000), .008);
            engine.Process(Pad(x: -20000), .008);
            await Drain();
            Check(delivered == 0 && discarded == 1 && (LightThemeCheck.IsChecked == true) == originalTheme,
                "stale Select survived a fresh pointer return and changed the actual option.");
            report.Add("PASS queued Select is discarded after fresh pointer return; the actual focused appearance checkbox does not change.");

            FocusSidebar(0);
            engine.Process(Pad(), .008); engine.Process(Pad(Buttons.Down), .008); engine.Process(Pad(), .008);
            engine.NotifyPointerInteraction(clicked: true);
            await Drain();
            Check(delivered == 0 && discarded == 1 && focusChanges == 0 && Pages.SelectedIndex == 0,
                "queued direction survived an explicit physical-click cancellation.");
            report.Add("PASS an explicit physical-click cancellation discards the queued direction without retargeting keyboard focus.");
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0,
                "the delivery fixture emitted a native key, shortcut or mouse click.");
            report.Add("These checks use command-aware production generation semantics and actual WPF focus through the engine/dispatcher boundary. Pointer output is recorded, not injected. Live Narrator listening and real controller input remain manual.");
            File.WriteAllLines(Path.Combine(output, "controller-navigation-delivery.txt"), report);
        }
        finally
        {
            engine.Reset(); RemoveHandler(Keyboard.GotKeyboardFocusEvent, changed);
            SelectPage(originalPage); await Drain();
        }
    }
}

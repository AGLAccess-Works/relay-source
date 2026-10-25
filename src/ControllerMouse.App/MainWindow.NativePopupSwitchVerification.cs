using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunNativePopupSwitchChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Native popup switch verification requires isolated Relay without desktop output.");
        var settings = _settings; var profiles = _app.Profiles; var status = _lastPresentationStatus;
        int page = Pages.SelectedIndex, starterIndex = StarterProfile.SelectedIndex;
        string input = _selectedInput;
        double width = Width, height = Height; var state = WindowState;
        bool loading = _loading, refreshing = _refresh.IsEnabled, saving = _save.IsEnabled;
        var sink = new NavigationVerificationSink(); var commands = new List<RelayNavigationCommand>();
        var report = new StringBuilder(); int cases = 0, targets = 0;
        var engine = new InputProcessor(sink)
        {
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command); Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        var preview = new ControllerStatus(true, true, true, 0, "Preview", default, null, false);
        Task Drain() => Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle).Task;
        int Highlight(ComboBox combo) => Keyboard.FocusedElement is ComboBoxItem item &&
            ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), combo)
            ? combo.ItemContainerGenerator.IndexFromContainer(item) : -1;
        void Check(bool condition, string detail)
        {
            if (condition) return;
            throw new InvalidOperationException("Native switch popup: " + detail +
                $" Focus {Keyboard.FocusedElement}; commands [{string.Join(", ", commands)}]; " +
                $"window {ActualWidth:0}×{ActualHeight:0}, scale {_settings.InterfaceScale:P0}.");
        }
        async Task Press(Buttons button, RelayNavigationCommand expected)
        {
            commands.Clear();
            for (int frame = 0; frame < 125; frame++) engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008); await Drain();
            Check(commands.SequenceEqual(new[] { expected }), "one held switch produced missing/duplicate or incorrect commands.");
        }
        void LoadFrame(double scale, bool switches)
        {
            _loading = true;
            _settings = (switches ? SetupPlan.Navigation(settings, SetupInputMethod.Switches) : settings with
            {
                ButtonMappings = null,
                ButtonChords = null,
                NarratorNavigation = true,
                ClickHoldMilliseconds = 0,
                Smoothing = 0
            })
                .WithVisualAccessScheme(DisplayScheme.RelayDefault) with
            { InterfaceScale = scale, ControllerType = ControllerPreference.Automatic };
            try { PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); }
            finally { _loading = false; }
            _lastPresentationSettings = null; RefreshStatus(preview); UpdateAccessibleViewport(); UpdateLayout();
            engine.Settings = _settings; engine.Process(default(Gamepad), .008);
        }
        _refresh.Stop(); _save.Stop();
        try
        {
            WindowState = WindowState.Normal;
            foreach (var context in new[] { (Width: 1440d, Height: 900d, Scale: 1d), (Width: 640d, Height: 480d, Scale: 2d) })
                foreach (var source in new[] { (Page: 9, Name: "Hardware layout", Combo: _hardwareLayout!), (Page: 8, Name: "Accessibility starter profile", Combo: StarterProfile) })
                {
                    Width = context.Width; Height = context.Height; LoadFrame(context.Scale, true);
                    SelectPage(source.Page); await Drain();
                    var combo = source.Combo;
                    combo.IsDropDownOpen = false; combo.SelectedIndex = 0;
                    combo.BringIntoView(); Check(combo.Focus(), "the actual " + source.Name + " field cannot receive focus."); await Drain();
                    Check(combo.Items.Count >= 3 && engine.FixedSwitchKeyboardLayout && HasFixedSwitchPopupControls(),
                        "the actual field lacks three choices or the exact four-switch assignments.");
                    int commits = 0;
                    SelectionChangedEventHandler changed = (_, _) => commits++;
                    combo.SelectionChanged += changed;
                    try
                    {
                        await Press(Buttons.A, RelayNavigationCommand.Select);
                        Check(combo.IsDropDownOpen && Highlight(combo) == 0, "A did not open at the actual selected native item.");
                        await Press(Buttons.X, RelayNavigationCommand.Previous);
                        Check(combo.IsDropDownOpen && Highlight(combo) == 0 && combo.SelectedIndex == 0 && commits == 0,
                            "X escaped the first popup endpoint or committed while exploring.");
                        await Press(Buttons.B, RelayNavigationCommand.Next);
                        Check(Highlight(combo) == 1 && combo.SelectedIndex == 0 && commits == 0, "B left the popup or committed the highlighted item.");
                        await Press(Buttons.X, RelayNavigationCommand.Previous);
                        Check(Highlight(combo) == 0 && combo.SelectedIndex == 0 && commits == 0, "X failed to reverse native choice exploration.");
                        await Press(Buttons.B, RelayNavigationCommand.Next);
                        await Press(Buttons.A, RelayNavigationCommand.Select);
                        Check(!combo.IsDropDownOpen && combo.SelectedIndex == 1 && commits == 1 && combo.IsKeyboardFocused,
                            "A did not commit the highlighted choice once and restore source focus.");
                        if (source.Page == 9) Check(_settings.ControllerType == ControllerPreference.Standard,
                            "Hardware layout selection did not reach the actual settings handler.");
                        else Check(StarterProfile.SelectedItem is AccessibilityPreset preset && StarterDescription.Text == preset.Description,
                            "Starter selection did not reach its actual description handler.");
                        cases++; report.AppendLine($"PASS {source.Name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: held A opens; X clamps first endpoint; B/X highlight without saved-value changes; held A commits once through actual field handler and restores source focus.");

                        commits = 0; await Press(Buttons.A, RelayNavigationCommand.Select);
                        Check(Highlight(combo) == 1, "reopening did not highlight the saved native choice.");
                        for (int index = 2; index < combo.Items.Count; index++)
                        {
                            await Press(Buttons.B, RelayNavigationCommand.Next);
                            Check(Highlight(combo) == index && combo.SelectedIndex == 1 && commits == 0,
                                "B skipped a native item or changed its saved value while exploring.");
                            var item = (ComboBoxItem)Keyboard.FocusedElement;
                            Check(item.ActualWidth >= 44 && item.ActualHeight >= 44 && AvailableNavigationElement(item),
                                "a keyboard-highlighted native choice lacks an available 44px target.");
                            targets++;
                        }
                        await Press(Buttons.B, RelayNavigationCommand.Next);
                        Check(combo.IsDropDownOpen && Highlight(combo) == combo.Items.Count - 1 && combo.SelectedIndex == 1 && commits == 0,
                            "B escaped the last popup endpoint or committed it.");
                        await Press(Buttons.X, RelayNavigationCommand.Previous);
                        Check(Highlight(combo) == combo.Items.Count - 2 && combo.SelectedIndex == 1 && commits == 0,
                            "X did not reverse from the last native item without committing.");
                        await Press(Buttons.Y, RelayNavigationCommand.Back);
                        Check(!combo.IsDropDownOpen && combo.SelectedIndex == 1 && commits == 0 && combo.IsKeyboardFocused && Pages.SelectedIndex == source.Page,
                            "Y committed the highlight, left the page or lost source focus instead of cancelling.");
                        cases++; report.AppendLine($"PASS {source.Name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: B reaches every native choice and clamps last endpoint; X reverses; Y closes only popup, preserves saved value and source/page focus.");

                        await Press(Buttons.A, RelayNavigationCommand.Select);
                        var order = MainNavigationOrder(NavigationControls((DependencyObject)Pages.SelectedContent).ToArray());
                        int sourceIndex = Array.FindIndex(order, target => ReferenceEquals(target, combo));
                        Check(sourceIndex >= 0 && sourceIndex + 1 < order.Length, "the source field has no ordinary next destination.");
                        commands.Clear();
                        Check(AccessibilitySupport.NavigateTab(this, false), "the physical Tab route is unavailable."); await Drain();
                        Check(order[sourceIndex + 1].IsKeyboardFocused && commands.Count == 0,
                            "physical Tab was changed into four-switch popup exploration.");
                        combo.IsDropDownOpen = false;
                        cases++; report.AppendLine($"PASS {source.Name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: physical Tab retains ordinary next-field traversal under fixed-switch settings.");
                    }
                    finally { combo.IsDropDownOpen = false; combo.SelectionChanged -= changed; }

                    LoadFrame(context.Scale, false); SelectPage(source.Page); await Drain();
                    AccessibilitySupport.FocusHeading(PageHeading);
                    await Press(Buttons.Up, RelayNavigationCommand.Up);
                    combo.BringIntoView(); Check(combo.Focus(), "the standard-controller source cannot receive focus."); await Drain();
                    await Press(Buttons.A, RelayNavigationCommand.Select);
                    Check(combo.IsDropDownOpen && !HasFixedSwitchPopupControls(), "standard-controller fixture did not open a native popup without fixed-switch ownership.");
                    var standardOrder = MainNavigationOrder(NavigationControls((DependencyObject)Pages.SelectedContent).ToArray());
                    int standardIndex = Array.FindIndex(standardOrder, target => ReferenceEquals(target, combo));
                    await Press(Buttons.RightShoulder, RelayNavigationCommand.Next);
                    Check(standardIndex >= 0 && standardIndex + 1 < standardOrder.Length && standardOrder[standardIndex + 1].IsKeyboardFocused,
                        "standard-controller RB was changed into switch-only native popup exploration.");
                    combo.IsDropDownOpen = false;
                    cases++; report.AppendLine($"PASS {source.Name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: standard-controller RB retains ordinary next-field behavior.");
                }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                "native popup navigation emitted desktop input.");
            File.WriteAllText(Path.Combine(output, "native-popup-switches.txt"),
                $"PASS {cases} actual native dropdown interactions and {targets} highlighted 44px targets at 100% and 200%. Four-switch B/X explore without committing; A applies once; Y cancels; endpoints, source focus and ordinary physical Tab/standard RB behavior preserved. No native desktop input, user storage writes or live Narrator/hardware acceptance is claimed.\n\n" + report);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "native-popup-switches-failure.txt"), error + "\n\nCompleted checks:\n" + report);
            throw;
        }
        finally
        {
            _hardwareLayout!.IsDropDownOpen = false; StarterProfile.IsDropDownOpen = false; engine.Reset();
            _settings = settings; _app.Profiles = profiles; _loading = true;
            try { PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); StarterProfile.SelectedIndex = starterIndex; }
            finally { _loading = loading; }
            Width = width; Height = height; WindowState = state; SelectPage(page);
            _lastPresentationSettings = null; RefreshStatus(status); SelectPresentedInput(input); UpdateAccessibleViewport(); await Drain();
            if (refreshing) _refresh.Start(); if (saving) _save.Start();
        }
    }
}

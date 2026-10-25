using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunShortcutCaptureChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Shortcut capture verification requires isolated Relay without desktop output.");
        var originalBinding = _capturedBinding;
        var report = new List<string>();
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Shortcut Tab capture: " + message);
        }
        try
        {
            foreach (double scale in new[] { 1d, 2d })
                foreach (var modifiers in new[] { ModifierKeys.None, ModifierKeys.Shift })
                {
                    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Exception? failure = null;
                    // Only the modifier-reading boundary is supplied by this fixture.
                    // Window preview routing, the TextBox recorder, focus movement and
                    // Cancel are the real production controls; no native key is sent.
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
                    {
                        Window? dialog = null;
                        try
                        {
                            dialog = Application.Current.Windows.OfType<Window>().Single(window => window.Owner == this &&
                                window.Title == "Record keyboard shortcut" && window.IsVisible);
                            await Drain(); dialog.UpdateLayout();
                            foreach (var (token, size) in new[] { ("MainFontSize", 16d), ("CaptionFontSize", 14d), ("ControlMinHeight", 44d) })
                                dialog.Resources[token] = size * scale;
                            dialog.SizeToContent = SizeToContent.Manual; dialog.Width = Math.Min(480, dialog.MaxWidth); dialog.Height = Math.Min(480, dialog.MaxHeight);
                            await Drain(); dialog.UpdateLayout();
                            var capture = NavigationInteractionDescendants(dialog).OfType<TextBox>().Single(text =>
                                AutomationProperties.GetName(text) == "Shortcut recorder");
                            var use = NavigationInteractionDescendants(dialog).OfType<Button>().Single(button => button.Content?.ToString() == "Use shortcut");
                            var cancel = NavigationInteractionDescendants(dialog).OfType<Button>().Single(button => button.Content?.ToString() == "Cancel");
                            var viewport = (ScrollViewer)dialog.Content;
                            var instruction = NavigationInteractionDescendants(dialog).OfType<TextBlock>().Single(text =>
                                AutomationProperties.GetAutomationId(text) == "ShortcutRecorderInstructions");
                            Check(dialog.FontSize == 16 * scale && capture.FontSize == 16 * scale && instruction.FontSize == 16 * scale &&
                                instruction.TextWrapping == TextWrapping.Wrap && dialog.ResizeMode != ResizeMode.NoResize,
                                "recorder typography or wrapping does not follow its scaled interface resources.");
                            Check(scale < 2 || viewport.ScrollableHeight > 0, "large recorder text has no available page scroll.");
                            foreach (var action in new FrameworkElement[] { instruction, capture, cancel })
                            {
                                action.BringIntoView(); dialog.UpdateLayout();
                                var bounds = action.TransformToAncestor(viewport).TransformBounds(new Rect(action.RenderSize));
                                Check(bounds.Top >= -1 && bounds.Bottom <= viewport.ViewportHeight + 1 && bounds.Left >= -1 && bounds.Right <= viewport.ViewportWidth + 1,
                                    "recorder instructions, capture field or Cancel cannot be fully brought into its visible viewport.");
                            }
                            capture.BringIntoView(); dialog.UpdateLayout();
                            Check(capture.IsKeyboardFocused && !use.IsEnabled, "recorder did not begin focused and waiting for a shortcut.");
                            var source = PresentationSource.FromVisual(capture)!;
                            var tab = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                            var changes = new List<IInputElement>();
                            KeyboardFocusChangedEventHandler changed = (_, e) => { if (e.NewFocus != null) changes.Add(e.NewFocus); };
                            dialog.AddHandler(Keyboard.GotKeyboardFocusEvent, changed, true);
                            try
                            {
                                capture.RaiseEvent(tab); await Drain();
                                Check(tab.Handled && _capturedBinding is { Kind: BindingKind.Keyboard, Key: 9 } binding &&
                                    binding.Modifiers == (modifiers == ModifierKeys.Shift ? 4 : 0) && binding.IsValid,
                                    "Tab was consumed as navigation or lost its Shift modifier instead of becoming a mapping.");
                                Check(use.IsEnabled && use.IsKeyboardFocused && changes.Count == 1 && changes[0] == use,
                                    "capture did not move once directly to its newly enabled Use shortcut action.");
                                use.BringIntoView(); dialog.UpdateLayout();
                                var useBounds = use.TransformToAncestor(viewport).TransformBounds(new Rect(use.RenderSize));
                                Check(use.ActualWidth >= 44 && use.ActualHeight >= 44 && useBounds.Top >= -1 &&
                                    useBounds.Bottom <= viewport.ViewportHeight + 1 && useBounds.Right <= viewport.ViewportWidth + 1,
                                    "the enabled Use shortcut action cannot be fully shown after recording at scaled text.");
                                changes.Clear();
                                var nextTab = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                                use.RaiseEvent(nextTab); await Drain();
                                Check(nextTab.Handled && cancel.IsKeyboardFocused && changes.Count == 1 && changes[0] == cancel,
                                    "leaving the recorder failed to restore shared ordinary Tab navigation.");
                            }
                            finally { dialog.RemoveHandler(Keyboard.GotKeyboardFocusEvent, changed); }
                            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(cancel)!.GetPattern(PatternInterface.Invoke)).Invoke();
                            await Drain();
                            Check(!dialog.IsVisible, "Cancel did not close the actual shortcut dialog.");
                            report.Add($"PASS {(modifiers == ModifierKeys.Shift ? "Shift+Tab" : "Tab")}, {scale:P0}, {dialog.ActualWidth:0}×{dialog.ActualHeight:0}: scaled instructions wrap; instructions, recorder, Use and Cancel are reachable within the page viewport; real Window/TextBox preview routing records key9 with intended modifiers and focuses Use once; ordinary Tab then reaches Cancel once. Modifier state supplied at a fake boundary; no native input.");
                        }
                        catch (Exception error) { failure = error; if (dialog?.IsVisible == true) dialog.Close(); }
                        finally { finished.TrySetResult(); }
                    }));
                    CaptureShortcut(this, () => modifiers);
                    await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    if (failure != null) throw failure;
                }
            await RunSwitchShortcutCaptureChecks(output);
            File.WriteAllLines(Path.Combine(output, "shortcut-tab-capture.txt"), report);
        }
        finally { _capturedBinding = originalBinding; }
    }

    private async Task RunSwitchShortcutCaptureChecks(string output)
    {
        var report = new List<string>();
        var originalBinding = _capturedBinding;
        var originalOptions = MappingAction.ItemsSource;
        var originalOption = MappingAction.SelectedItem;
        var originalFocus = Keyboard.FocusedElement;
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Switch shortcut recorder: " + detail);
        }
        try
        {
            foreach (string scenario in new[] { "Cancel selection", "Back from recorder", "Use physical shortcut", "Already paused" })
            {
                var sink = new NavigationVerificationSink();
                var commands = new List<RelayNavigationCommand>();
                var engine = new InputProcessor(sink) { Settings = _settings, RelayWindowActive = true };
                bool enabled = scenario != "Already paused";
                engine.SetEnabled(enabled);
                var applied = new List<(Settings Settings, bool Configuration)>();
                engine.RelayNavigationRequested = command => { commands.Add(command); NavigateController(command); };
                var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Exception? failure = null;
                _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
                {
                    Window? dialog = null;
                    try
                    {
                        dialog = Application.Current.Windows.OfType<Window>().Single(window => window.Owner == this &&
                            window.Title == "Record keyboard shortcut" && window.IsVisible);
                        await Drain();
                        var capture = NavigationInteractionDescendants(dialog).OfType<TextBox>().Single(text =>
                            AutomationProperties.GetName(text) == "Shortcut recorder");
                        var use = NavigationInteractionDescendants(dialog).OfType<Button>().Single(button => button.Content?.ToString() == "Use shortcut");
                        var cancel = NavigationInteractionDescendants(dialog).OfType<Button>().Single(button => button.Content?.ToString() == "Cancel");
                        Check(engine.ConfigurationMode && engine.FixedSwitchKeyboardLayout && engine.Enabled == enabled &&
                            applied.Count == 1 && applied[0].Configuration,
                            "opening did not apply tool-free A/B/X/Y navigation or changed the prior pause state.");
                        static Gamepad Pad(Buttons buttons = Buttons.None, short left = 0) => new(buttons, 0, 0, left, 0, 0, 0);
                        async Task Press(Buttons button, int frames = 1)
                        {
                            engine.Process(Pad(), .008);
                            for (int frame = 0; frame < frames; frame++) engine.Process(Pad(button), .008);
                            engine.Process(Pad(), .008);
                            await Drain();
                        }
                        Check(capture.IsKeyboardFocused && !use.IsEnabled, "recorder did not start focused and empty.");
                        if (!enabled)
                        {
                            await Press(Buttons.B);
                            Check(cancel.IsKeyboardFocused && !engine.Enabled &&
                                commands.SequenceEqual(new[] { RelayNavigationCommand.Next }),
                                "a previously paused owned recorder could not reach Cancel using B.");
                            await Press(Buttons.X);
                            Check(capture.IsKeyboardFocused && !engine.Enabled,
                                "paused X could not return to the shortcut recorder.");
                            await Press(Buttons.Y);
                            Check(!engine.Enabled && _capturedBinding == null && commands.SequenceEqual(new[] {
                                RelayNavigationCommand.Next, RelayNavigationCommand.Previous, RelayNavigationCommand.Back }),
                                "paused cancellation resumed desktop control or recorded a synthetic shortcut.");
                        }
                        else
                        {
                            await Press(Buttons.A, 125);
                            Check(capture.IsKeyboardFocused && !use.IsEnabled && _capturedBinding == null &&
                                commands.SequenceEqual(new[] { RelayNavigationCommand.Select }),
                                "controller Select recorded a synthetic Enter, moved focus or repeated.");
                            if (scenario == "Back from recorder")
                            {
                                await Press(Buttons.Y);
                                Check(_capturedBinding == null, "controller Back recorded a synthetic Escape.");
                            }
                            else if (scenario == "Cancel selection")
                            {
                                await Press(Buttons.B);
                                Check(cancel.IsKeyboardFocused, "B could not reach Cancel without a keyboard or bumper.");
                                await Press(Buttons.X);
                                Check(capture.IsKeyboardFocused, "X could not return to the recorder without native keys.");
                                await Press(Buttons.B); await Press(Buttons.A);
                            }
                            else
                            {
                                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(capture)!,
                                    Environment.TickCount, Key.K)
                                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                                capture.RaiseEvent(key); await Drain();
                                Check(key.Handled && _capturedBinding is { Kind: BindingKind.Keyboard, Key: 75, Modifiers: 1 } &&
                                    use.IsEnabled && use.IsKeyboardFocused, "physical Ctrl+K failed to enable and focus Use shortcut.");
                                // Losing foreground ownership must quarantine all mapped output.
                                engine.RelayWindowActive = false;
                                engine.Process(Pad(Buttons.A, 22000), .008);
                                Check(!engine.Armed && sink.MoveCalls == 0 && sink.Keys.Count == 0 && sink.Shortcuts.Count == 0,
                                    "recording configuration leaked movement or a key to another foreground application.");
                                engine.RelayWindowActive = true;
                                await Press(Buttons.A);
                            }
                        }
                        await Drain();
                        Check(!dialog.IsVisible, "switch cancellation or Use did not close the real modal.");
                    }
                    catch (Exception error) { failure = error; if (dialog?.IsVisible == true) dialog.Close(); }
                    finally { finished.TrySetResult(); }
                }));
                CaptureShortcut(this, () => ModifierKeys.Control, (settings, configuration) =>
                {
                    applied.Add((settings, configuration));
                    engine.Settings = settings;
                    engine.ConfigurationMode = configuration;
                    // This fake boundary models the actual opted-in modal WPF HWND.
                    engine.PausedNavigationWindowActive = configuration;
                    engine.Reset();
                });
                await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (failure != null) throw failure;
                Check(applied.Count == 2 && !applied[1].Configuration && applied[1].Settings == _settings &&
                    engine.Settings.PointerSpeed == _settings.PointerSpeed &&
                    ControlMapping.Controls.All(option => ControlMapping.Resolve(engine.Settings, option.Control) == ControlMapping.Resolve(_settings, option.Control)) &&
                    !engine.ConfigurationMode && engine.Enabled == enabled && !engine.Armed,
                    "closing failed to restore original settings/configuration/pause state or require neutral input.");
                int before = commands.Count;
                engine.Process(new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0), .008);
                Check(!engine.Armed && commands.Count == before, "a held selection crossed the restore boundary.");
                Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                    "recorder navigation or restoration emitted native output.");
                if (scenario == "Use physical shortcut")
                    Check(MappingAction.SelectedItem is BindingOption { Binding: { Kind: BindingKind.Keyboard, Key: 75, Modifiers: 1 } },
                        "Use shortcut did not retain the recorded Ctrl+K binding.");
                report.Add($"PASS {scenario}: real modal routes A/B/X/Y through the temporary configuration layout; selection and Back do not record synthetic keys; original settings and pause state restored; neutral required after close; no native output.");
                engine.Reset();
            }
            File.WriteAllLines(Path.Combine(output, "shortcut-switch-navigation.txt"), report);
        }
        finally
        {
            _capturedBinding = originalBinding;
            MappingAction.ItemsSource = originalOptions; MappingAction.SelectedItem = originalOption;
            if (originalFocus is UIElement { IsVisible: true } element) element.Focus();
            else AccessibilitySupport.FocusHeading(PageHeading);
            await Drain();
        }
    }
}

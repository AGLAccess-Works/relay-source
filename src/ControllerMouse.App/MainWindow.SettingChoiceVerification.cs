using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private sealed record SettingChoiceFixtureOption(string Name, int Value, string Description);

    private async Task RunSettingChoiceChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Setting-choice verification requires isolated Relay without desktop output.");
        var originalSettings = _settings; var originalProfiles = _app.Profiles;
        int originalPage = Pages.SelectedIndex;
        double originalWidth = Width, originalHeight = Height; var originalState = WindowState;
        bool originalLoading = _loading, originalExpanded = _moreVisualOptions?.IsExpanded == true;
        bool originalFeedbackExpanded = ReadingFeedbackOptions.IsExpanded;
        bool refreshing = _refresh.IsEnabled, saving = _save.IsEnabled;
        var originalFocus = Keyboard.FocusedElement;
        var sink = new NavigationVerificationSink(); var commands = new List<RelayNavigationCommand>();
        var report = new StringBuilder(); int cases = 0, targets = 0, directCases = 0, directTargets = 0;
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { NarratorNavigation = true, ClickHoldMilliseconds = 0 },
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command); Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        engine.Process(default(Gamepad), .008);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Setting choices: " + message +
                $" Focus {Keyboard.FocusedElement}; commands [{string.Join(", ", commands)}].");
        }
        IInvokeProvider InvokePattern(Button button)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
            Check(peer != null && !string.IsNullOrWhiteSpace(peer.GetName()) &&
                !string.IsNullOrWhiteSpace(peer.GetAutomationId()), "actual choice action lacks its accessible name or identifier.");
            return peer!.GetPattern(PatternInterface.Invoke) as IInvokeProvider ??
                throw new InvalidOperationException("Setting choice action lacks native Invoke.");
        }
        async Task Press(Buttons button)
        {
            commands.Clear();
            for (int frame = 0; frame < 125; frame++) engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008); await Drain();
            Check(commands.Count == 1, "one held controller edge produced missing or duplicate commands.");
        }
        void CaptureChoice(Window window, string file)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); using var stream = File.Create(Path.Combine(output, file));
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        }
        _refresh.Stop(); _save.Stop(); _loading = true;
        try
        {
            WindowState = WindowState.Normal;
            foreach (var context in new[] { (Width: 1440d, Height: 900d, Scale: 1d, Name: "desktop-100"), (Width: 640d, Height: 480d, Scale: 2d, Name: "small-200") })
            {
                Width = context.Width; Height = context.Height;
                _settings = originalSettings with { InterfaceScale = context.Scale };
                SelectPage(3); UpdateAccessibleViewport(); PopulateVisualAccessOptions();
                Check(_moreVisualOptions != null, "More display options section is missing.");
                _moreVisualOptions!.IsExpanded = false;
                ReadingFeedbackOptions.IsExpanded = false;
                ((ScrollViewer)Pages.SelectedContent).ScrollToHome(); UpdateLayout(); await Drain();
                Check(new FrameworkElement?[] { _themeChoice, _textSizeChoice, _spacingChoice, _complexityChoice, _focusChoice }.All(choice => choice?.IsVisible == true) &&
                    _oneControlChoice?.IsVisible == true, "simple display overview omitted an appearance, text or simplicity control.");
                Check(new FrameworkElement?[] { _schemeChoice, _motionChoice, _highlightChoice }.All(choice => choice?.IsVisible == false),
                    "collapsed advanced display options remained in the visible page.");
                foreach (var row in new[] { _themeChoice!, _textSizeChoice!, _spacingChoice!, _complexityChoice!, _focusChoice! })
                    Check(row.ChoiceButtons.Count == row.Items.Count && row.ChoiceButtons.All(button => InvokePattern(button) != null),
                        "a frequent preference is missing its actual direct option buttons.");
                Check(!NavigationInteractionDescendants(VisualAccessHost).OfType<ComboBox>().Any(choice => choice.IsVisible),
                    "display overview still contains a visible dropdown.");
                CaptureChoice(this, "display-settings-overview-" + context.Name + ".png");

                var options = new[] {
                    new SettingChoiceFixtureOption("Relay Default",0,"Purple and gold with clear, separate selected and pressed states."),
                    new("Soft Light",1,"Softer surfaces with readable dark text."),
                    new("Deep Dark",2,"Dark purple surfaces with readable light text."),
                    new("Maximum Contrast Light",3,"Light surfaces with strong boundaries and dark text."),
                    new("Maximum Contrast Dark",4,"Dark surfaces with strong boundaries and light text."),
                    new("CVI Simplified",5,"A simpler starting point. Change the preferences to suit you."),
                    new("Custom",6,"Choose your own colours while keeping text and focus readable.") };
                var setting = new SettingChoiceButton { SettingName = "Display scheme", ItemsSource = options, SelectedIndex = 2, Margin = new Thickness(24) };
                AutomationProperties.SetAutomationId(setting, "SettingChoiceFixture");
                var host = new Window
                {
                    Owner = this,
                    Title = "Relay setting-choice verification",
                    Width = context.Width,
                    Height = context.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = setting
                };
                host.SetResourceReference(FontSizeProperty, "MainFontSize"); host.SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
                host.SetResourceReference(BackgroundProperty, "SurfaceBackgroundBrush"); host.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
                AccessibilitySupport.EnableKeyboardAccess(host);
                var commits = new List<int>(); setting.SelectionChanged += (_, _) => commits.Add(setting.SelectedIndex);
                try
                {
                    host.Show(); host.UpdateLayout(); setting.Focus(); await Drain();
                    async Task Run(string name, int? committed, Action open, Func<SettingChoiceWindow, Task> interact, int? expectedIndex = null)
                    {
                        setting.ItemsSource = options; setting.SelectedIndex = 2; commits.Clear(); setting.Focus(); await Drain();
                        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Exception? failure = null;
                        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
                        {
                            SettingChoiceWindow? dialog = null;
                            try
                            {
                                dialog = setting.ActiveChoiceWindow;
                                Check(dialog != null && dialog.IsVisible && dialog.Owner == host, "Button activation did not open its actual owned choice window.");
                                await Drain(); dialog!.UpdateLayout();
                                Check(dialog.ChoiceButtons.Count == options.Length && dialog.ChoiceButtons[2].IsKeyboardFocused &&
                                    dialog.SelectedIndex == 2, "choice window did not focus the current option without committing.");
                                var currentPeer = UIElementAutomationPeer.CreatePeerForElement(dialog.ChoiceButtons[2])!;
                                Check(currentPeer.GetName() == options[2].Name && currentPeer.GetItemStatus() == "Current choice" &&
                                    NavigationInteractionDescendants(dialog.ChoiceButtons[2]).OfType<TextBlock>().Any(text => text.Text.Contains("✓ Current choice", StringComparison.Ordinal)),
                                    "current option lacks its readable name, selected status or visible check text.");
                                Check(commits.Count == 0 && setting.SelectedIndex == 2, "opening a choice changed its saved value.");
                                foreach (var action in dialog.ChoiceButtons.Append(dialog.CancelButton))
                                {
                                    InvokePattern(action); action.BringIntoView(); dialog.UpdateLayout();
                                    var bounds = action.TransformToAncestor(dialog.ChoicesViewport).TransformBounds(new Rect(action.RenderSize));
                                    Check(action.ActualWidth >= 44 && action.ActualHeight >= 44 && bounds.Top >= -1 &&
                                        bounds.Bottom <= dialog.ChoicesViewport.ViewportHeight + 1 && bounds.Left >= -1 &&
                                        bounds.Right <= dialog.ChoicesViewport.ViewportWidth + 1, "a real option or Cancel cannot be fully shown at readable text size.");
                                    targets++;
                                }
                                Check(context.Scale < 2 || dialog.ChoicesViewport.ScrollableHeight > 0, "large choices do not have a usable scroll.");
                                dialog.ChoiceButtons[2].BringIntoView(); dialog.ChoiceButtons[2].Focus(); await Drain();
                                if (name == "controller cancel") CaptureChoice(dialog, "setting-choice-" + context.Name + ".png");
                                await interact(dialog); await Drain();
                                Check(!dialog.IsVisible && setting.ActiveChoiceWindow == null && setting.IsKeyboardFocused,
                                    "closing the choice did not restore focus to its original setting summary.");
                                int expected = committed ?? 2, position = expectedIndex ?? expected;
                                Check(setting.SelectedIndex == position && Equals(setting.SelectedValue, expected) &&
                                    setting.CurrentDisplayName == options[expected].Name && commits.Count == (committed.HasValue ? 1 : 0) &&
                                    (!committed.HasValue || commits[0] == position), "choice committed zero/multiple times, changed on Cancel, or lost its logical value/summary after a live list change.");
                                var summary = UIElementAutomationPeer.CreatePeerForElement(setting)!;
                                Check(summary.GetName().Contains(options[expected].Name, StringComparison.Ordinal) &&
                                    summary.GetItemStatus().Contains(options[expected].Name, StringComparison.Ordinal), "restored setting summary omitted its current value from UIA.");
                                cases++; report.AppendLine($"PASS {name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: named current option initially focused; all options/Cancel are44px and scroll reachable; expected commit count; source focus and summary restored.");
                            }
                            catch (Exception error) { failure = error; if (dialog?.IsVisible == true) dialog.Close(); }
                            finally { finished.TrySetResult(); }
                        }));
                        open(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); await Drain();
                        if (failure != null) throw failure;
                    }
                    void OpenUia() => InvokePattern(setting).Invoke();
                    await Run("controller cancel", null, OpenUia, async dialog =>
                    {
                        await Press(Buttons.Up); Check(dialog.ChoiceButtons[1].IsKeyboardFocused, "D-pad Up skipped the previous option.");
                        await Press(Buttons.Down); Check(dialog.ChoiceButtons[2].IsKeyboardFocused, "D-pad Down skipped the next option.");
                        await Press(Buttons.RightShoulder); Check(dialog.ChoiceButtons[3].IsKeyboardFocused, "RB did not reach the next option.");
                        await Press(Buttons.LeftShoulder); Check(dialog.ChoiceButtons[2].IsKeyboardFocused, "LB did not reverse option order.");
                        for (int index = 3; index < options.Length; index++) { await Press(Buttons.Down); Check(dialog.ChoiceButtons[index].IsKeyboardFocused, "D-pad could not explore every option."); }
                        await Press(Buttons.RightShoulder); Check(dialog.CancelButton.IsKeyboardFocused, "RB could not reach Cancel after the last option.");
                        Check(setting.SelectedIndex == 2 && commits.Count == 0, "exploring choices changed the saved setting.");
                        await Press(Buttons.B);
                    });
                    await Run("keyboard cancel", null, () => DispatchControlKey(setting, Key.Enter), async dialog =>
                    {
                        Check(DispatchControlKey(dialog.ChoiceButtons[2], Key.Down) && dialog.ChoiceButtons[3].IsKeyboardFocused, "real routed keyboard Down did not explore the next option.");
                        Check(DispatchControlKey(dialog.ChoiceButtons[3], Key.Up) && dialog.ChoiceButtons[2].IsKeyboardFocused, "real routed keyboard Up did not reverse option order.");
                        DispatchControlKey(dialog.ChoiceButtons[2], Key.Escape); await Drain();
                    });
                    await Run("Cancel action", null, OpenUia, async dialog => { InvokePattern(dialog.CancelButton).Invoke(); await Drain(); });
                    await Run("UIA choice", 6, OpenUia, async dialog => { InvokePattern(dialog.ChoiceButtons[6]).Invoke(); await Drain(); });
                    await Run("pointer handler choice", 0, OpenUia, async dialog =>
                    {
                        dialog.ChoiceButtons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, dialog.ChoiceButtons[0])); await Drain();
                    });
                    await Run("controller Select", 3, OpenUia, async dialog => { await Press(Buttons.Down); await Press(Buttons.A); });
                    await Run("keyboard Enter", 1, OpenUia, async dialog =>
                    {
                        DispatchControlKey(dialog.ChoiceButtons[2], Key.Up); Check(dialog.ChoiceButtons[1].IsKeyboardFocused, "keyboard Up did not focus the intended choice.");
                        DispatchControlKey(dialog.ChoiceButtons[1], Key.Enter); await Drain();
                    });
                    await Run("live list reorder", 6, OpenUia, async dialog =>
                    {
                        setting.ItemsSource = new[] { options[6] }.Concat(options.Take(6)).ToArray();
                        Check(Equals(setting.SelectedValue, 2) && commits.Count == 0,
                            "reordering the live source changed its saved logical value before a choice was applied.");
                        InvokePattern(dialog.ChoiceButtons[6]).Invoke(); await Drain();
                    }, expectedIndex: 0);
                    await Run("displayed choice removed", null, OpenUia, async dialog =>
                    {
                        setting.ItemsSource = options.Take(6).ToArray();
                        Check(Equals(setting.SelectedValue, 2) && commits.Count == 0,
                            "removing another live option changed the current saved value.");
                        InvokePattern(dialog.ChoiceButtons[6]).Invoke(); await Drain();
                    });
                }
                finally { setting.ActiveChoiceWindow?.Close(); host.Close(); await Drain(); }

                var directOptions = new[] {
                    new SettingChoiceFixtureOption("Standard\n100%",0,"Standard text."),
                    new("125%",1,"A little larger."),new("Large\n150%",2,"Large text."),
                    new("175%",3,"Very large text."),new("Extra large\n200%",4,"Extra large text.") };
                var direct = new DirectSettingChoiceRow("Text size", "DirectSettingFixture") { ItemsSource = directOptions, SelectedIndex = 2, Margin = new Thickness(24) };
                var directViewport = new ScrollViewer
                {
                    Content = direct,
                    Focusable = false,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                var directHost = new Window
                {
                    Owner = this,
                    Title = "Relay direct-choice verification",
                    Width = context.Width,
                    Height = context.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = directViewport
                };
                directHost.SetResourceReference(FontSizeProperty, "MainFontSize"); directHost.SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
                directHost.SetResourceReference(BackgroundProperty, "SurfaceBackgroundBrush"); directHost.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
                AccessibilitySupport.EnableKeyboardAccess(directHost);
                var directCommits = new List<int>(); direct.SelectionChanged += (_, _) => directCommits.Add(direct.SelectedIndex);
                try
                {
                    directHost.Show(); directHost.UpdateLayout(); await Drain();
                    Check(direct.ChoiceButtons.Count == 5, "direct text row lost a supported text level.");
                    foreach (var button in direct.ChoiceButtons)
                    {
                        InvokePattern(button); button.BringIntoView(); directHost.UpdateLayout(); await Drain();
                        var bounds = button.TransformToAncestor(directViewport).TransformBounds(new Rect(button.RenderSize));
                        Check(button.ActualWidth >= 44 && button.ActualHeight >= 44 && bounds.Top >= -1 &&
                            bounds.Bottom <= directViewport.ViewportHeight + 1 && bounds.Left >= -1 &&
                            bounds.Right <= directViewport.ViewportWidth + 1, "a direct preference option cannot be fully shown as a readable44px target.");
                        directTargets++;
                    }
                    direct.ChoiceButtons[2].BringIntoView(); direct.ChoiceButtons[2].Focus(); await Drain();
                    CaptureChoice(directHost, "direct-setting-choice-" + context.Name + ".png");
                    async Task RunDirect(string name, int? expected, Func<Task> action)
                    {
                        direct.SelectedIndex = 2; directCommits.Clear();
                        direct.ChoiceButtons[2].BringIntoView(); direct.ChoiceButtons[2].Focus(); await Drain();
                        Check(UIElementAutomationPeer.CreatePeerForElement(direct.ChoiceButtons[2])!.GetItemStatus() == "Selected" &&
                            NavigationInteractionDescendants(direct.ChoiceButtons[2]).OfType<TextBlock>().Any(text => text.Text.StartsWith("✓ ", StringComparison.Ordinal)),
                            "direct current option is missing its selected state or visible check.");
                        await action(); await Drain();
                        Check(direct.SelectedIndex == (expected ?? 2) && Equals(direct.SelectedValue, expected ?? 2) &&
                            directCommits.Count == (expected.HasValue && expected.Value != 2 ? 1 : 0),
                            "direct navigation changed a setting or selecting committed zero/multiple times.");
                        Check(directHost.IsVisible && direct.ChoiceButtons.Any(button => button.IsKeyboardFocused) &&
                            !Application.Current.Windows.OfType<SettingChoiceWindow>().Any(window => window.IsVisible),
                            "a direct option opened a picker or lost the source row's focus.");
                        if (expected.HasValue)
                        {
                            Check(direct.ChoiceButtons[expected.Value].IsKeyboardFocused &&
                                UIElementAutomationPeer.CreatePeerForElement(direct.ChoiceButtons[expected.Value])!.GetItemStatus() == "Selected",
                                "direct selection did not retain focus and expose its new current state.");
                            Check(direct.ChoiceButtons.Where((_, index) => index != expected.Value).All(button =>
                                UIElementAutomationPeer.CreatePeerForElement(button)!.GetItemStatus() == "Available choice"),
                                "a direct row exposed more than one current option.");
                        }
                        directCases++; report.AppendLine($"PASS direct {name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: explicit single commit; selected text/status; direct source focus retained without a modal picker.");
                    }
                    await RunDirect("controller exploration", null, async () =>
                    {
                        await Press(Buttons.Down); Check(direct.ChoiceButtons[3].IsKeyboardFocused, "D-pad Down skipped a direct option.");
                        await Press(Buttons.Up); Check(direct.ChoiceButtons[2].IsKeyboardFocused, "D-pad Up skipped a direct option.");
                        await Press(Buttons.RightShoulder); Check(direct.ChoiceButtons[3].IsKeyboardFocused, "RB skipped a direct option.");
                        await Press(Buttons.LeftShoulder); Check(direct.ChoiceButtons[2].IsKeyboardFocused, "LB skipped a direct option.");
                        await Press(Buttons.Right); Check(direct.ChoiceButtons[3].IsKeyboardFocused, "D-pad Right skipped the adjacent direct option.");
                        await Press(Buttons.Left); Check(direct.ChoiceButtons[2].IsKeyboardFocused, "D-pad Left skipped the adjacent direct option.");
                        Check(directCommits.Count == 0 && direct.SelectedIndex == 2, "exploring a direct row committed a value.");
                    });
                    await RunDirect("UI Automation", 0, async () =>
                    {
                        direct.ChoiceButtons[0].Focus(); InvokePattern(direct.ChoiceButtons[0]).Invoke(); await Drain();
                    });
                    await RunDirect("controller Select", 3, async () => { await Press(Buttons.RightShoulder); await Press(Buttons.A); });
                    await RunDirect("keyboard Enter", 1, async () =>
                    {
                        direct.ChoiceButtons[1].Focus(); DispatchControlKey(direct.ChoiceButtons[1], Key.Enter); await Drain();
                    });
                    await RunDirect("keyboard Space", 4, async () =>
                    {
                        var button = direct.ChoiceButtons[4]; button.Focus(); DispatchControlKey(button, Key.Space);
                        var source = PresentationSource.FromVisual(button) ?? throw new InvalidOperationException("Direct option has no keyboard presentation source.");
                        var release = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
                        button.RaiseEvent(release);
                        if (!release.Handled) { release.RoutedEvent = Keyboard.KeyUpEvent; button.RaiseEvent(release); }
                        await Drain();
                    });
                    await RunDirect("current option", 2, async () => { await Press(Buttons.A); });
                    await RunDirect("equivalent preference sync", 3, async () =>
                    {
                        await Press(Buttons.RightShoulder); await Press(Buttons.A);
                        var focused = direct.ChoiceButtons[3]; direct.ItemsSource = directOptions.Select(option => option with { }).ToArray();
                        await Drain(); Check(ReferenceEquals(focused, direct.ChoiceButtons[3]) && focused.IsKeyboardFocused,
                            "preference synchronization replaced the focused direct option.");
                    });
                }
                finally { directHost.Close(); await Drain(); }
            }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0, "choice navigation emitted native keyboard or pointer output.");
            File.WriteAllText(Path.Combine(output, "setting-choice-checks.txt"), $"PASS {cases} real modal choice interactions and {targets} named44px target/viewport checks across desktop100% and640×480/200%. Button Invoke, current-choice focus/status, safe Cancel/Back, single commit, D-pad/RB/LB/Select, routed keyboard arrows/Enter/Escape, live source reorder/removal preserving logical values and restored source summaries checked. Pointer handler uses a real routed Click event; no native input, real pointer hardware, settings-file write or Narrator listening is claimed.\n\n" + report);
            File.WriteAllText(Path.Combine(output, "direct-setting-choice-checks.txt"), $"PASS {directCases} direct preference interactions and {directTargets} named44px target/viewport checks across desktop100% and640×480/200%. All five text levels, native Button Invoke, selected status/check text, D-pad/RB/LB exploration without changes, explicit Select/Enter/Space, same-choice stability and equivalent-list focus retention checked. No modal picker, native input, real pointer hardware, settings-file write or Narrator listening is claimed.\n\n" + report);
        }
        finally
        {
            engine.Reset(); _app.Profiles = originalProfiles; _settings = originalSettings;
            Width = originalWidth; Height = originalHeight; WindowState = originalState;
            PopulateControls(); PopulateProfiles(); PopulateVisualAccessOptions(); SelectPage(originalPage); UpdateAccessibleViewport();
            if (_moreVisualOptions != null) _moreVisualOptions.IsExpanded = originalExpanded;
            ReadingFeedbackOptions.IsExpanded = originalFeedbackExpanded;
            _loading = originalLoading; UpdateLayout();
            if (originalFocus is UIElement focus && AvailableNavigationElement(focus)) focus.Focus(); else AccessibilitySupport.FocusHeading(PageHeading);
            if (refreshing) _refresh.Start(); if (saving) _save.Start(); await Drain();
        }
    }
}

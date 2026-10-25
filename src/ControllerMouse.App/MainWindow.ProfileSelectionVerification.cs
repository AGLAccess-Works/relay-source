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
using System.Windows.Threading;
using ControllerMouse.Core;
using Control = ControllerMouse.Core.Control;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunProfileSelectionChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Profile selection verification requires isolated Relay without desktop output.");
        var settings = _settings; var profiles = _app.Profiles;
        var status = _lastPresentationStatus; int page = Pages.SelectedIndex;
        string selectedInput = _selectedInput;
        bool loading = _loading, refreshing = _refresh.IsEnabled, saving = _save.IsEnabled;
        double width = Width, height = Height; var windowState = WindowState;
        var sink = new NavigationVerificationSink(); var commands = new List<RelayNavigationCommand>();
        var engine = new InputProcessor(sink)
        {
            Settings = SetupPlan.Navigation(settings, SetupInputMethod.Switches),
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command); Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        int cases = 0, targets = 0, explicitChoices = 0;
        var report = new StringBuilder();
        SelectionChangedEventHandler committed = (_, _) => { if (!_profileLoading) explicitChoices++; };
        ProfileCombo.SelectionChanged += committed;
        Task Drain() => Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle).Task;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Profile selection: " + message +
                $" Active {_app.Profiles.ActiveId}; focus {Keyboard.FocusedElement}; commands [{string.Join(", ", commands)}].");
        }
        IInvokeProvider Invoke(Button button)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
            Check(peer != null && !string.IsNullOrWhiteSpace(peer.GetName()) &&
                !string.IsNullOrWhiteSpace(peer.GetAutomationId()), "a profile choice lacks its name or ID.");
            return peer!.GetPattern(PatternInterface.Invoke) as IInvokeProvider ??
                throw new InvalidOperationException("Profile action has no native Invoke pattern.");
        }
        async Task Press(Buttons button)
        {
            commands.Clear();
            engine.Settings = _settings;
            engine.Process(default(Gamepad), .008);
            for (int frame = 0; frame < 125; frame++) engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008); await Drain();
            Check(commands.Count == 1, "a held switch did not produce exactly one navigation/selection command.");
        }
        _refresh.Stop(); _save.Stop(); _loading = false;
        try
        {
            WindowState = WindowState.Normal;
            foreach (var context in new[] { (Width: 1440d, Height: 900d, Scale: 1d), (Width: 640d, Height: 480d, Scale: 2d) })
            {
                Width = context.Width; Height = context.Height;
                var interfacePreferences = settings.WithVisualAccessScheme(DisplayScheme.CviSimplified) with { InterfaceScale = context.Scale };
                var firstBinding = new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.Narrator);
                var secondBinding = new ControlBinding(BindingKind.Shortcut, Shortcut: Shortcut.VoiceTyping);
                var switchSettings = SetupPlan.Navigation(settings, SetupInputMethod.Switches);
                var firstMappings = new Dictionary<Control, ControlBinding>(switchSettings.ButtonMappings ?? []) { [Control.Start] = firstBinding };
                var secondMappings = new Dictionary<Control, ControlBinding>(switchSettings.ButtonMappings ?? []) { [Control.Start] = secondBinding };
                _app.Profiles = new ProfileLibrary(1, "profile-access-a", [
                    new("profile-access-a","My switch controls",switchSettings with { PointerSpeed = 650, ButtonMappings = firstMappings }),
                    new("profile-access-b","Reading and typing",switchSettings with { PointerSpeed = 1200, ButtonMappings = secondMappings }),
                    new("profile-access-c","A long profile name for comfortable reading and switch setup",settings with { PointerSpeed = 1700 })
                ])
                { SetupCompleted = true, AccessibilityRevision = 1 };
                Check(_app.Profiles.Profiles.All(profile => ProfileLibrary.ValidName(profile.Name)), "the fixture uses an invalid profile name.");
                _settings = _app.Profiles.Active.Settings.WithInterfacePreferences(interfacePreferences);
                PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); SelectPage(8);
                UpdateAccessibleViewport(); await Drain();
                engine.Reset(); engine.Process(default(Gamepad), .008); explicitChoices = 0;
                var savedVisual = _settings.EffectiveVisualAccess;
                void Unchanged(string detail)
                {
                    Check(_app.Profiles.ActiveId == "profile-access-a" && _settings.PointerSpeed == 650 &&
                        ControlMapping.Resolve(_settings, Control.Start) == firstBinding && _settings.EffectiveVisualAccess == savedVisual &&
                        _settings.InterfaceScale == context.Scale, detail + " changed live controls or reading preferences.");
                }
                async Task OpenHeader(string name, bool apply)
                {
                    ProfileCombo.BringIntoView(); Check(ProfileCombo.Focus(), "header profile action is not focusable."); await Drain();
                    int commitsBefore = explicitChoices;
                    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Exception? failure = null;
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
                    {
                        SettingChoiceWindow? dialog = null;
                        try
                        {
                            dialog = ProfileCombo.ActiveChoiceWindow;
                            Check(dialog?.IsVisible == true && dialog.Owner == this, "A did not open the actual owned profile choice window.");
                            await Drain();
                            Check(dialog!.ChoiceButtons.Count == 3 && dialog.ChoiceButtons[0].IsKeyboardFocused,
                                "the current profile is not the initial focus.");
                            foreach (var button in dialog.ChoiceButtons.Append(dialog.CancelButton))
                            {
                                Invoke(button); button.BringIntoView(); dialog.UpdateLayout();
                                var bounds = button.TransformToAncestor(dialog.ChoicesViewport).TransformBounds(new Rect(button.RenderSize));
                                Check(button.ActualWidth >= 44 && button.ActualHeight >= 44 && bounds.Top >= -1 &&
                                    bounds.Bottom <= dialog.ChoicesViewport.ViewportHeight + 1 && bounds.Left >= -1 &&
                                    bounds.Right <= dialog.ChoicesViewport.ViewportWidth + 1, "an actual choice/Cancel is too small or clipped at large text.");
                                targets++;
                            }
                            dialog.ChoiceButtons[0].BringIntoView(); dialog.ChoiceButtons[0].Focus(); await Drain();
                            Unchanged("Opening profiles");
                            await Press(Buttons.B); Check(dialog.ChoiceButtons[1].IsKeyboardFocused, "B failed to reach the next profile choice.");
                            Unchanged("B exploration");
                            await Press(Buttons.X); Check(dialog.ChoiceButtons[0].IsKeyboardFocused, "X failed to reverse profile exploration.");
                            Unchanged("X exploration");
                            await Press(Buttons.B);
                            if (apply) await Press(Buttons.A);
                            else
                            {
                                await Press(Buttons.B); Check(dialog.ChoiceButtons[2].IsKeyboardFocused, "B missed the last profile choice.");
                                await Press(Buttons.B); Check(dialog.CancelButton.IsKeyboardFocused, "B missed Cancel after the last profile.");
                                Unchanged("Browsing every profile and Cancel"); await Press(Buttons.Y);
                            }
                            await Drain();
                            Check(!dialog.IsVisible && ProfileCombo.ActiveChoiceWindow == null && ProfileCombo.IsKeyboardFocused,
                                "closing profiles lost the original source-button focus.");
                            if (apply) Check(_app.Profiles.ActiveId == "profile-access-b" && _settings.PointerSpeed == 1200 &&
                                ControlMapping.Resolve(_settings, Control.Start) == secondBinding && engine.FixedSwitchKeyboardLayout && explicitChoices == commitsBefore + 1 &&
                                _settings.EffectiveVisualAccess == savedVisual && _settings.InterfaceScale == context.Scale,
                                "explicit A did not commit one profile while preserving reading preferences.");
                            else { Unchanged("Y cancellation"); Check(explicitChoices == commitsBefore, "Cancel committed a profile."); }
                            cases++; report.AppendLine($"PASS {name}, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: actual A opens owned profile choices; B/X explore all options and Cancel without switching; {(apply ? "A commits once" : "Y cancels without commit")}; source focus, mappings and reading preferences preserved.");
                        }
                        catch (Exception error) { failure = error; if (dialog?.IsVisible == true) dialog.Close(); }
                        finally { finished.TrySetResult(); }
                    }));
                    await Press(Buttons.A); await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); await Drain();
                    if (failure != null) throw failure;
                }
                await OpenHeader("four-switch cancel", false);
                await OpenHeader("four-switch activate", true);
                var rows = ProfileList.Children.OfType<Button>().ToArray();
                Check(rows.Length == 3 && !NavigationInteractionDescendants(ProfileList).Any(node => node is ListBox or ComboBox),
                    "Profiles still contains an auto-committing native selection list.");
                var viewport = (ScrollViewer)Pages.SelectedContent;
                var contentViewport = LayoutVisualElements(viewport).OfType<ScrollContentPresenter>().First();
                foreach (var row in rows)
                {
                    Invoke(row); row.BringIntoView(); await Drain();
                    // The page ScrollViewer has 26px padding; its rendering
                    // viewport starts inside that padding. Measure against the
                    // actual presenter, not zero in the outer chrome coordinates.
                    var bounds = row.TransformToAncestor(contentViewport).TransformBounds(new Rect(row.RenderSize));
                    string geometry = $"{context.Width:0}×{context.Height:0}/{context.Scale:P0}; {AutomationProperties.GetName(row)}; " +
                        $"target {row.ActualWidth:0.0}×{row.ActualHeight:0.0}; bounds {bounds}; viewport {contentViewport.ActualWidth:0.0}×{contentViewport.ActualHeight:0.0}; " +
                        $"scroll viewport {viewport.ViewportWidth:0.0}×{viewport.ViewportHeight:0.0}; padding {viewport.Padding}";
                    File.AppendAllText(Path.Combine(output, "profile-row-geometry.txt"), geometry + Environment.NewLine);
                    Check(row.ActualWidth >= 44 && row.ActualHeight >= 44 && bounds.Top >= -1 &&
                        bounds.Bottom <= contentViewport.ActualHeight + 1 && bounds.Left >= -1 &&
                        bounds.Right <= contentViewport.ActualWidth + 1, "a saved profile row is too small or cannot scroll fully into view. " + geometry);
                    targets++;
                }
                rows[0].BringIntoView(); Check(rows[0].Focus(), "first profile row is not focusable."); await Drain();
                await Press(Buttons.B); Check(rows[1].IsKeyboardFocused && _app.Profiles.ActiveId == "profile-access-b", "B browse changed the profile.");
                await Press(Buttons.B); Check(rows[2].IsKeyboardFocused && _app.Profiles.ActiveId == "profile-access-b", "B skipped a saved profile or activated it.");
                await Press(Buttons.X); Check(rows[1].IsKeyboardFocused && _settings.PointerSpeed == 1200, "X browse changed live settings.");
                await Press(Buttons.X); Check(rows[0].IsKeyboardFocused && _app.Profiles.ActiveId == "profile-access-b", "X failed to reach the first saved profile.");
                await Press(Buttons.A);
                Check(_app.Profiles.ActiveId == "profile-access-a" && _settings.PointerSpeed == 650 &&
                    ControlMapping.Resolve(_settings, Control.Start) == firstBinding && engine.FixedSwitchKeyboardLayout && rows[0].IsKeyboardFocused &&
                    ReferenceEquals(rows[0], ProfileList.Children[0]) && _settings.EffectiveVisualAccess == savedVisual &&
                    _settings.InterfaceScale == context.Scale, "A row activation changed focus, mappings or reading preferences incorrectly.");
                Check(AutomationProperties.GetItemStatus(rows[0]) == "Current profile" &&
                    AutomationProperties.GetItemStatus(rows[1]) == "Available profile" &&
                    Equals(ProfileCombo.SelectedValue, "profile-access-a"), "current profile labels/header are inconsistent after activation.");
                cases++; report.AppendLine($"PASS saved profile rows, {context.Width:0}×{context.Height:0}/{context.Scale:P0}: ordinary named native Invoke buttons; B/X focus every row without switching live controls; held A activates explicitly, same button focus retained; current labels/header and independent reading preferences consistent.");
            }
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                "profile exploration emitted native desktop input.");
            File.WriteAllText(Path.Combine(output, "profile-selection-access.txt"),
                $"PASS {cases} actual profile interactions and {targets} 44px/scroll-reachable targets at desktop 100% and small-window 200%. Four-switch exploration never changes live profiles; explicit activation/cancel preserve controls and independent interface preferences. No native desktop input, user storage writes or live Narrator/hardware acceptance is claimed.\n\n" + report);
        }
        finally
        {
            ProfileCombo.ActiveChoiceWindow?.Close(); ProfileCombo.SelectionChanged -= committed; engine.Reset();
            _settings = settings; _app.Profiles = profiles; _loading = true;
            try { PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); }
            finally { _loading = loading; }
            Width = width; Height = height; WindowState = windowState;
            SelectPage(page); _lastPresentationSettings = null; RefreshStatus(status); SelectPresentedInput(selectedInput);
            UpdateAccessibleViewport(); UpdateLayout(); await Drain();
            if (refreshing) _refresh.Start(); if (saving) _save.Start();
        }
    }
}

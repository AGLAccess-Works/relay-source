using System;
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
    private async Task RunNarratorNavigationChecks(string output)
    {
        bool topmost = Topmost; Topmost = true; Activate();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        try
        {
            static void Enter(UIElement target)
            {
                var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("No keyboard event source.");
                target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            }
            static void F6(UIElement target)
            {
                target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, Key.F6)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            }
            for (int page = 0; page < Pages.Items.Count; page++)
            {
                SelectPage(page);
                if (!PageHeading.IsKeyboardFocused || AutomationProperties.GetHeadingLevel(PageHeading) != AutomationHeadingLevel.Level1 ||
                    UIElementAutomationPeer.CreatePeerForElement(PageHeading)?.GetName()?.StartsWith(_pageNames[page], StringComparison.Ordinal) != true)
                    throw new InvalidOperationException($"Page {page} does not expose its heading and real keyboard focus.");
                F6(PageHeading);
                var navigation = CurrentNavigation();
                if (!navigation.IsKeyboardFocused) throw new InvalidOperationException("F6 did not reach page navigation.");
                F6(navigation);
                if (!PageHeading.IsKeyboardFocused) throw new InvalidOperationException("F6 did not return to content.");
                if (!PageHeading.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)) ||
                    Keyboard.FocusedElement is not System.Windows.Controls.Control next || !next.IsVisible || !next.IsEnabled)
                    throw new InvalidOperationException("Tab cannot reach an enabled visible control after the heading.");
            }
            await RunControllerNavigationChecks(output);
            await RunNavigationDeliveryChecks(output);
            SelectPage(5); NarratorNavigationCheck.Focus();
            bool original = _settings.NarratorNavigation;
            var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(NarratorNavigationCheck)!.GetPattern(PatternInterface.Toggle);
            Enter(NarratorNavigationCheck);
            if (_settings.NarratorNavigation == original || toggle.ToggleState != (_settings.NarratorNavigation ? ToggleState.On : ToggleState.Off))
                throw new InvalidOperationException("Controller Enter did not update the check box, persisted setting and UI Automation state together.");
            Enter(NarratorNavigationCheck);
            if (_settings.NarratorNavigation != original) throw new InvalidOperationException("Repeated selection cannot undo the setting.");
            // Use the same Toggle pattern screen readers use, not a synthetic click.
            foreach (var (check, page, read) in new (CheckBox, int, Func<bool>)[] {
                (TremorCheck, 2, () => _settings.TremorStabilization), (TargetCheck, 2, () => _settings.TargetAssist),
                (ScrollAxisLockCheck, 5, () => _settings.ScrollAxisLock), (ScrollStabilizationCheck, 5, () => _settings.ScrollStabilization),
                (ScrollModifiersCheck, 5, () => _settings.ScrollModifiers), (ScrollPrecisionCheck, 5, () => _settings.ScrollPrecision),
                (InvertCheck, 5, () => _settings.InvertScroll), (NarratorNavigationCheck, 5, () => _settings.NarratorNavigation),
                (HapticsCheck, 5, () => _settings.Haptics), (MagnifierHapticsCheck, 5, () => _settings.MagnifierHaptics),
                (ScrollHapticsCheck, 5, () => _settings.ScrollHaptics), (TargetHapticsCheck, 5, () => _settings.TargetHaptics),
                (BoundaryHapticsCheck, 5, () => _settings.BoundaryHaptics), (LightThemeCheck, 7, () => _settings.LightTheme),
                (TrayCheck, 7, () => _settings.MinimizeToTray),
                (ShowLabelsCheck, 3, () => _settings.ShowControllerLabels),
                (ReduceMotionCheck, 3, () => _settings.ReduceInterfaceMotion),
                (InputAnnouncementsCheck, 3, () => _settings.InputAnnouncements),
                (ReadHoveredTextCheck, 3, () => _settings.ReadHoveredText) })
            {
                SelectPage(page);
                if (page == 3) { ReadingFeedbackOptions.IsExpanded = true; UpdateLayout(); }
                bool before = read();
                var provider = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(check)!.GetPattern(PatternInterface.Toggle);
                provider.Toggle();
                if (read() == before || check.IsChecked != read()) throw new InvalidOperationException($"Automation toggle did not apply {check.Name} to settings.");
                provider.Toggle();
                if (read() != before) throw new InvalidOperationException($"Automation toggle did not restore {check.Name}.");
            }
            SelectPage(2);
            TargetCheck.IsChecked = true;
            foreach (var (check, read) in new (CheckBox, Func<bool>)[] { (HintCheck, () => _settings.ShowTargetHint), (RememberCheck, () => _settings.RememberTargets) })
            {
                bool before = read();
                var provider = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(check)!.GetPattern(PatternInterface.Toggle);
                provider.Toggle();
                if (read() == before) throw new InvalidOperationException($"Automation toggle did not apply {check.Name}.");
                provider.Toggle();
            }
            TargetCheck.IsChecked = false;
            var radio = NavigationButtons.Children.OfType<RadioButton>().First(button => button.Tag?.ToString() == "6");
            radio.Focus(); Enter(radio);
            if (Pages.SelectedIndex != 6 || radio.IsChecked != true) throw new InvalidOperationException("Enter cannot select a page radio button.");

            SelectPage(8);
            _settings = _settings with { AdaptiveAccessories = [new("A", "My click switch", AccessoryKind.Switch, [AccessorySignal.A])] };
            StarterProfile.SelectedItem = AccessibilityProfiles.All.First(preset => preset.Settings.ControllerType == ControllerPreference.Adaptive);
            AddStarterProfile(this, new RoutedEventArgs());
            if (_settings.AdaptiveAccessories?.Single().Name != "My click switch")
                throw new InvalidOperationException("Adaptive starter erased the recorded accessory setup.");

            _app.Profiles = ProfileLibrary.Create(new Settings()); _settings = new Settings(); PopulateControls(); PopulateProfiles(); ReadSettings();
            SelectPage(0);
            RefreshStatus(); CaptureRelay(output, "welcome-narrator");
            File.WriteAllText(Path.Combine(output, "narrator-navigation.txt"),
                $"PASS: all {Pages.Items.Count} headings expose heading metadata and real focus; F6 returns between navigation and content; Tab reaches visible controls; Enter updates checkbox UI Automation ToggleState and the setting; 21 options apply their actual setting through UIA Toggle and restore it; Enter selects a page; adding an Adaptive starter preserves recorded accessories. Live Narrator speech, scan mode, braille and physical accessories require manual beta acceptance.");
        }
        finally { Topmost = topmost; }
    }
}

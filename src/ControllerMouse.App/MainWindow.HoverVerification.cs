using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunHoverReadingChecks(string output)
    {
        var report = new List<string>();
        var readings = new List<(FrameworkElement Target, string Text)>();
        bool enabled = true;
        int clicks = 0;
        var focusAnchor = new Button { Content = "Keep keyboard focus here" };
        var child = new TextBlock { Text = "Visual reconnect label" };
        var reconnect = new Button { Content = child };
        AutomationProperties.SetName(reconnect, "Reconnect controller");
        reconnect.Click += (_, _) => clicks++;
        var option = new CheckBox { Content = "Show controller labels", IsChecked = true };
        var note = new TextBlock { Text = "Release all controls before continuing.", TextWrapping = TextWrapping.Wrap };
        var unavailable = new Button { Content = "Unavailable action", IsEnabled = false };
        var password = new PasswordBox { Password = "fixture-secret-never-read" };
        AutomationProperties.SetName(password, "Account password");
        var combo = new ComboBox { ItemsSource = new[] { "First profile", "Second profile" }, SelectedIndex = 0 };
        AutomationProperties.SetName(combo, "Profile");
        var stack = new StackPanel { Margin = new Thickness(20) };
        foreach (var item in new FrameworkElement[] { focusAnchor, reconnect, option, note, unavailable, password, combo })
            stack.Children.Add(item);
        var dialog = new Window
        {
            Title = "Relay hover verification",
            Owner = this,
            Content = stack,
            Width = 640,
            Height = 620,
            Topmost = true,
            ShowActivated = true
        };
        using var reader = new RelayHoverReader(dialog, () => enabled,
            (target, text) => readings.Add((target, text)));
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Hover reading: " + message);
        }
        try
        {
            dialog.Show(); dialog.Activate();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            focusAnchor.Focus();
            Check(dialog.IsActive, "owned test window is not active.");
            reader.Observe(child, new Point(10, 10));
            Check(readings.Count == 0, "announcement fired before the dwell.");
            await Task.Delay(RelayHoverReader.Dwell + TimeSpan.FromMilliseconds(100));
            // Timer ticks run at Background priority. After a busy navigation
            // suite, let the dispatcher drain before inspecting that real tick.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(readings.Count == 1 && readings[0].Target == reconnect &&
                readings[0].Text == "Reconnect controller", $"child label did not resolve to its actual named button; active={dialog.IsActive}, pending={reader.HasPendingRequest}, readings={string.Join(" | ", readings.Select(reading => reading.Text))}.");
            Check(focusAnchor.IsKeyboardFocused && clicks == 0, "hover changed focus or invoked a button.");
            report.Add("PASS real dwell timer resolves a child label to its named UIA button, without focus, selection or click changes.");

            reader.Observe(child, new Point(10, 10)); reader.CompleteDwell();
            Check(readings.Count == 1, "stationary unchanged hover repeated.");
            reader.Observe(child, new Point(20, 20));
            reader.Observe(option, new Point(40, 40)); reader.CompleteDwell();
            Check(readings.Count == 2 && readings.Last().Target == option && readings.Last().Text.Contains("On"),
                "latest checkbox target/state was not read.");
            Check(option.IsChecked == true && focusAnchor.IsKeyboardFocused, "hover changed an option or focus.");
            report.Add("PASS only the latest target is read; actual checkbox state is exposed without toggling it; unchanged hover is suppressed.");

            reader.Observe(note, new Point(60, 60)); reader.CompleteDwell();
            Check(readings.Last().Target == note && readings.Last().Text == note.Text, "standalone help text was not exposed.");
            reader.Observe(unavailable, new Point(80, 80)); reader.CompleteDwell();
            Check(readings.Last().Text.Contains("Unavailable"), "disabled state was not read.");
            reader.Observe(password, new Point(100, 100)); reader.CompleteDwell();
            Check(readings.Last().Text == "Account password. Password field." &&
                readings.All(reading => !reading.Text.Contains(password.Password)), "password contents were exposed.");
            report.Add("PASS standalone help text and disabled state are readable; password values are never included.");

            int beforeCancel = readings.Count;
            reader.Observe(note, new Point(120, 120)); reader.Cancel();
            reader.Observe(note, new Point(120, 120)); reader.CompleteDwell();
            Check(readings.Count == beforeCancel, "cancelled hover restarted without deliberate pointer movement.");
            reader.Observe(note, new Point(160, 160)); reader.CompleteDwell();
            Check(readings.Count == beforeCancel + 1, "new pointer movement did not resume reading.");
            reader.Observe(child, new Point(200, 200)); option.Focus(); reader.CompleteDwell();
            Check(readings.Count == beforeCancel + 1 && option.IsKeyboardFocused,
                "pending hover survived a real keyboard-focus change.");
            reader.Observe(option, new Point(240, 240)); reader.CompleteDwell();
            Check(readings.Count == beforeCancel + 1, "already-focused control was read again.");
            report.Add("PASS keyboard/controller cancellation discards pending hover and waits for fresh pointer movement; already-focused controls are suppressed.");

            combo.Focus(); combo.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            async Task CloseChoice()
            {
                var closed = new TaskCompletionSource<bool>();
                EventHandler onClosed = (_, _) => closed.TrySetResult(true);
                popup.Closed += onClosed;
                try
                {
                    int beforeClosing = readings.Count;
                    combo.IsDropDownOpen = false;
                    // The closing animation can keep the visual item alive.
                    // Closed logical choices must already be ineligible to read.
                    reader.CompleteDwell();
                    Check(readings.Count == beforeClosing, "closing choice item was read during its animation.");
                    await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
                    Check(closed.Task.IsCompletedSuccessfully, "actual choice popup did not finish closing.");
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                }
                finally { popup.Closed -= onClosed; }
            }
            Check(popup.Child != null && reader.ObservesRoot(popup.Child), "choice popup has no hover event owner.");
            Check(RelayPointerObservation.ObservesChoicePopup(combo), "choice popup has no native pointer observer.");
            var second = (ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(1);
            reader.Observe(second, new Point(300, 300)); reader.CompleteDwell();
            Check(readings.Last().Target == second && readings.Last().Text.Contains("Second profile") &&
                combo.SelectedIndex == 0, "hover selected a drop-down item or used the parent field's label.");
            int beforeClose = readings.Count;
            second.Content = "Updated profile";
            reader.Observe(second, new Point(340, 340));
            Check(reader.HasPendingRequest, "popup cancellation fixture did not prepare a changed unread target.");
            await CloseChoice(); reader.CompleteDwell();
            Check(!reader.ObservesRoot(popup.Child!) && readings.Count == beforeClose,
                "closed popup retained a hook or pending announcement.");
            Check(!RelayPointerObservation.ObservesChoicePopup(combo), "closed popup retained its native pointer hook.");
            combo.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(reader.ObservesRoot(popup.Child!) && RelayPointerObservation.ObservesChoicePopup(combo),
                "reopened choice popup did not restore its hover and native pointer owners.");
            await CloseChoice();
            Check(!reader.ObservesRoot(popup.Child!) && !RelayPointerObservation.ObservesChoicePopup(combo),
                "reopened popup did not detach its observers again.");
            report.Add("PASS actual drop-down popup has its own hover hooks, uses item text without selection, and detaches/cancels when closed; reopen restores and closes both observers again.");

            focusAnchor.Focus(); reader.Observe(note, new Point(400, 400)); enabled = false; reader.CompleteDwell();
            Check(readings.Count == beforeClose, "disabled reading still announced.");
            enabled = true; reader.Observe(note, new Point(440, 440)); reader.Dispose(); reader.CompleteDwell();
            Check(readings.Count == beforeClose && !reader.ObservesRoot(dialog), "disposed window retained hover or event hooks.");
            report.Add("PASS preference changes, window disposal and popup closure cancel pending reading.");
        }
        finally { dialog.Close(); Activate(); AccessibilitySupport.FocusHeading(PageHeading); }
        report.Add("The notification transport uses the screen reader's UIA notification API. This fixture records the announcement boundary; actual Narrator speech and native mouse-mode coexistence still require listening acceptance. No native input or user settings were changed.");
        File.WriteAllLines(Path.Combine(output, "hover-reading.txt"), report);
    }
}

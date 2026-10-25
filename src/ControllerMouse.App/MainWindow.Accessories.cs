using System;
using System.Linq;
using System.Windows;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void OpenAccessories(object sender, RoutedEventArgs e) => EditAccessories(sender, false);
    private void OpenIdentifyInput(object sender, RoutedEventArgs e) => EditAccessories(sender, true);
    private void EditAccessories(object sender, bool identifyFirst)
    {
        var returnControl = sender as System.Windows.Controls.Control;
        if (returnControl is Window || returnControl == null)
            returnControl = System.Windows.Input.Keyboard.FocusedElement as System.Windows.Controls.Control;
        var dialog = new AdaptiveAccessoriesWindow(_settings.AdaptiveAccessories,
            () => _app.Service?.Status is { Connected: true, Suspended: false } status ? status.State : null,
            capture => _app.Service?.SetAccessoryCapture(capture), identifyFirst)
        { Owner = this };
        try { dialog.ShowDialog(); }
        finally
        {
            _app.Service?.SetAccessoryCapture(false);
        }
        if (dialog.Result != null)
        {
            _settings = _settings with { AdaptiveAccessories = dialog.Result }; ReadSettings(); SaveSettings();
        }
        if (returnControl is { IsVisible: true, IsEnabled: true })
        {
            System.Windows.Automation.AutomationProperties.SetItemStatus(returnControl,
                dialog.Result == null ? "Setup cancelled. Your accessories are unchanged." : _saveError == null
                    ? $"Setup saved. {dialog.Result.Length} accessories."
                    : "Accessory setup is applied for this session. Settings could not be saved.");
            returnControl.Focus();
        }
    }
    private void RefreshAccessories(Gamepad state, bool connected)
    {
        var entries = _settings.AdaptiveAccessories ?? [];
        ControllerArt.Accessories = MappingArt.Accessories = entries;
        bool adaptive = _app.Service?.ControllerKind == ControllerKind.Adaptive || _settings.ControllerType == ControllerPreference.Adaptive;
        AccessoryPanel.Visibility = adaptive || entries.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        AccessorySummary.Text = entries.Length == 0 ? "No accessories added yet. Choose Set up accessories to name each device and socket." :
            string.Join("\n", entries.Select(a => a.Name + " (" + a.Kind + ") · " + AdaptiveAccessory.Ports.First(p => p.Id == a.Port).Name + " · " +
                (connected && a.Signals.Any(s => AdaptiveAccessory.Active(s, state)) ? "Matching input active" : a.Signals.Length == 0 ? "Input not recorded" : "Saved setup")));
    }
    private async System.Threading.Tasks.Task VerifyAccessories(string output)
    {
        Gamepad pad = default; var states = new System.Collections.Generic.List<bool>();
        var window = new AdaptiveAccessoriesWindow(null, () => pad, capture => states.Add(capture)) { Owner = this, Topmost = true };
        try { window.Show(); await window.VerifyForTest(output, value => pad = value); }
        finally { window.Close(); }
        if (!states.SequenceEqual(new[] { true, false })) throw new InvalidOperationException("Accessory capture was not safely bracketed.");
        var original = _settings; int originalPage = Pages.SelectedIndex; bool originalPorts = _portsView;
        try
        {
            _settings = _settings with
            {
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [
                new("A","Left foot pedal",AccessoryKind.Pedal,[AccessorySignal.A]),
                new("USBLeft","Chin joystick",AccessoryKind.Joystick,[AccessorySignal.LeftAxis]) ]
            };
            Pages.SelectedIndex = 0; SetPortsView(true);
            var preview = new ControllerStatus(true, true, true, 0, "Battery unavailable", new(Buttons.A, 0, 0, 20000, 0, 0, 0), null, false);
            RefreshStatus(preview); UpdateLayout();
            if (!LiveSurface.Adaptive || !LiveSurface.PortsView || LiveSurface.ActualWidth <= 0 || LiveSurface.ActualHeight <= 0)
                throw new InvalidOperationException("The visible adaptive controller surface was unavailable for accessory verification.");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(LiveSurface.ActualWidth), (int)Math.Ceiling(LiveSurface.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(LiveSurface);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "adaptive-accessory-diagram.png"))) encoder.Save(file);
            if (!AccessorySummary.Text.Contains("Matching input active") || !AccessorySummary.Text.Contains("Chin joystick")) throw new InvalidOperationException("Saved accessory feedback was missing.");
            if (LiveSurface.GetPart("A") is not ControllerPartButton { HardwareActive: true } || LiveSurface.GetPart("USBLeft") is not ControllerPartButton { HardwareActive: true })
                throw new InvalidOperationException("The composed controller surface did not reflect saved matching input activity.");
        }
        finally
        {
            _settings = original; SetPortsView(originalPorts); Pages.SelectedIndex = originalPage; RefreshStatus();
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "adaptive-accessories.txt"), "PASS: simulated neutral/press recording with output quarantine callback; saved accessory entry and result; UI Automation names and Invoke/Selection/Value patterns; 480x480 scrollable editor; diagram and matching-input feedback. No physical accessories detected or native inputs injected.");
    }

}

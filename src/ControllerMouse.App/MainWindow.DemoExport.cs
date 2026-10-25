using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    // Uses the real UI in the existing isolated preview. It never creates a
    // ControllerService, injects desktop input, or writes personal preferences.
    private async Task ExportDemoFrames(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Demo export requires isolated preview mode.");
        WindowState = WindowState.Normal; Width = 1600; Height = 900;
        _settings = new Settings { ControllerType = ControllerPreference.Standard };
        _app.ApplyVisualAccess(_settings); PopulateControls(); PopulateProfiles();
        var status = new ControllerStatus(true, true, true, 0, "Preview", default, null, false);
        async Task Frame(string name, Gamepad pad = default, bool connected = true, bool enabled = true)
        {
            RefreshStatus(status with { State = pad, Connected = connected, Enabled = enabled });
            Footer.Text = "Proof of concept · simulated controller input · no Windows input sent";
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            CaptureRelay(output, name);
        }
        void Invoke(System.Windows.Controls.Primitives.ButtonBase button) => ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!
            .GetPattern(PatternInterface.Invoke)).Invoke();
        SelectPage(0); await Frame("01-disconnected", connected: false);
        var wizard = new SetupWindow(_settings, _ => null, _ => { }) { Owner = this, Width = 760, Height = 780 };
        wizard.Show();
        try { await wizard.ExportDemoPages(output); } finally { wizard.Close(); }
        await Frame("06-standard-ready");
        foreach (var (name, pad) in new[] {
            ("07-standard-move", new Gamepad(Buttons.None,0,0,18000,9000,0,0)),
            ("08-standard-click", new Gamepad(Buttons.A,0,0,0,0,0,0)),
            ("09-standard-right-click", new Gamepad(Buttons.B,0,0,0,0,0,0)),
            ("10-standard-fine", new Gamepad(Buttons.Right,0,0,0,0,0,0)),
            ("11-standard-scroll", new Gamepad(Buttons.None,0,0,0,0,0,-17000)) }) await Frame(name, pad);
        SelectPage(1); PracticeTargets.BringIntoView(); await Frame("12-pointer-before");
        ResetPractice(this, new RoutedEventArgs());
        int hit = 0;
        foreach (var target in PracticeTargets.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<Button>()))
        {
            Invoke(target); PracticeTargets.BringIntoView(); await Frame($"13-practice-{++hit}");
        }
        if (_practiceHits != 5) throw new InvalidOperationException("Demo practice did not activate all five real targets.");
        foreach (var (page, name) in new[] { (5,"14-scrolling"),(6,"15-mapping"),(2,"16-assistance"),
            (3,"17-accessibility"),(8,"18-profiles"),(9,"19-hardware"),(7,"20-settings"),(4,"21-help") })
        { SelectPage(page); await Frame(name); }
        SelectPage(0); await Frame("22-paused", new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0), enabled: false);
        await Frame("23-resumed");
        _settings = _settings with
        {
            ControllerType = ControllerPreference.Adaptive,
            AdaptiveAccessories = [
            new("A", "Click switch", AccessoryKind.Switch, [AccessorySignal.A]),
            new("USBLeft", "Chin joystick", AccessoryKind.Joystick, [AccessorySignal.LeftAxis]) ]
        };
        PopulateControls(); SetPortsView(false); await Frame("24-adaptive-front");
        SetPortsView(true); await Frame("25-adaptive-ports");
        await Frame("26-adaptive-switch", new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0));
        await Frame("27-adaptive-joystick", new Gamepad(Buttons.None, 0, 0, 19000, 0, 0, 0));
        _settings = SetupPlan.Build(new SetupChoices { InputMethod = SetupInputMethod.Switches, Keyboard = true, SimpleControls = false }, _settings);
        PopulateControls(); SetPortsView(false); await Frame("28-switch-layout");
        foreach (var (button, name, command) in new[] {
            (Buttons.B,"29-switch-next",RelayNavigationCommand.Next),
            (Buttons.X,"30-switch-previous",RelayNavigationCommand.Previous) })
        {
            NavigateController(command); await Frame(name, new Gamepad(button, 0, 0, 0, 0, 0, 0));
        }
        _settings = SetupPlan.Build(new SetupChoices { InputMethod = SetupInputMethod.Pedals, Keyboard = true, SimpleControls = false }, _settings);
        PopulateControls(); await Frame("31-pedal-layout");
        await Frame("32-pedal-zoom-out", new Gamepad(Buttons.None, 190, 0, 0, 0, 0, 0));
        await Frame("33-pedal-zoom-in", new Gamepad(Buttons.None, 0, 190, 0, 0, 0, 0));
        var typed = new System.Text.StringBuilder();
        var keyboard = new KeyboardWindow(keys =>
        {
            foreach (ushort key in keys) if (key >= 0x41 && key <= 0x5a) typed.Append((char)key);
            return true;
        }, fixedSwitchControls: () => true);
        try
        {
            keyboard.ShowNearPointer(); keyboard.Width = 1200; keyboard.Height = 440;
            int index = 0;
            foreach (char letter in "RELAY")
            {
                int attempts = 0;
                while (keyboard.SelectedKey != letter && attempts++ < 100)
                    keyboard.ControllerCommand(Shortcut.KeyboardNext);
                if (keyboard.SelectedKey != letter) throw new InvalidOperationException("Demo cannot reach keyboard letter.");
                keyboard.ControllerCommand(Shortcut.KeyboardShift);
                keyboard.ControllerCommand(Shortcut.KeyboardSelect);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                keyboard.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)keyboard.ActualWidth, (int)keyboard.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(keyboard);
                using var file = File.Create(Path.Combine(output, $"34-keyboard-{++index}.png"));
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                File.WriteAllText(Path.Combine(output, $"34-keyboard-{index}.txt"), typed.ToString());
            }
            if (typed.ToString() != "RELAY") throw new InvalidOperationException("Demo keyboard output mismatch.");
        }
        finally { keyboard.Close(); }
        File.WriteAllText(Path.Combine(output, "demo-evidence.txt"),
            "Real Relay WPF UI rendered at 1600x900. Simulated ControllerStatus previews; no physical hardware or Windows action outcomes are claimed. Five real practice buttons invoked and their actual score checked. Real keyboard key Invoke handlers delivered RELAY to an isolated fake output callback, checked exactly. Switch menu navigation invoked in the real Relay window. No ControllerService created; no desktop input sent; no personal settings saved. Product controller artwork is illustrative. Physical-controller footage is still required for hardware acceptance evidence.");
    }
}

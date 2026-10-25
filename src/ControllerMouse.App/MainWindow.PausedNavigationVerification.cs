using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ControllerMouse.Core;
using Control = ControllerMouse.Core.Control;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunPausedNavigationChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Paused navigation requires isolated Relay and recorded desktop output.");
        int originalPage = Pages.SelectedIndex;
        var originalFocus = Keyboard.FocusedElement;
        var report = new List<string>();
        var native = new List<NativeMethods.Input>();
        var external = new List<string>();
        var commands = new List<RelayNavigationCommand>();
        var settings = new Settings
        {
            NarratorNavigation = false,
            ButtonMappings = new()
            {
                [Control.A] = new(BindingKind.Keyboard, Key: 13),
                [Control.B] = new(BindingKind.Keyboard, Key: 9),
                [Control.X] = new(BindingKind.Keyboard, Key: 9, Modifiers: 4),
                [Control.Y] = new(BindingKind.Keyboard, Key: 27)
            }
        };
        var sink = new WindowsInputSink(inputs => { native.AddRange(inputs); return (uint)inputs.Length; }, external.Add);
        using var service = new ControllerService(settings, Path.Combine(output, "paused-navigation-memory", "target-memory.json"),
            "paused-navigation-verification", sink);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        static Gamepad Pad(Buttons button = Buttons.None, short x = 0) => new(button, 0, 0, x, 0, 0, 0);
        void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Paused controller navigation: " + detail);
        }
        async Task Press(Buttons button, int frames = 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                service.ProcessVerificationInput(Pad(button), handle);
                await Drain();
            }
            service.ProcessVerificationInput(Pad(), handle); await Drain();
        }
        service.RelayNavigationRequested += command =>
        {
            long generation = service.CaptureRelayNavigationGeneration(command);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!service.IsRelayNavigationCurrent(command, generation)) return;
                commands.Add(command); NavigateController(command);
            }));
        };
        try
        {
            bool rejectedNative = false;
            try { using var rejected = new ControllerService(settings, verificationSink: new WindowsInputSink()); }
            catch (InvalidOperationException) { rejectedNative = true; }
            Check(rejectedNative, "synthetic controller transport accepted a real native-output sink.");
            report.Add("PASS the isolated service rejects an uninjected native WindowsInputSink; its synthetic input hook requires the supplied recording transport and a stopped hardware worker.");
            _app.SetVerificationService(service);
            PausedRelayWindowNavigation.Attach(this); // The main HWND already exists in this fixture.
            Check(service.OwnsPausedNavigationWindow(handle), "the existing main WPF HWND was not registered.");
            SelectPage(0); service.ProcessVerificationInput(Pad(), handle); RefreshStatus(service.Status); UpdateLayout();
            PauseButton.BringIntoView(); UpdateLayout(); PauseButton.Focus(); await Drain();
            Check(PauseButton.IsKeyboardFocused && PauseButton.IsVisible && service.Status.Enabled,
                "the actual Pause control was not initially reachable.");
            await Press(Buttons.A, 25);
            Check(!service.Status.Enabled && PauseButton.Content?.ToString() == "Resume control" &&
                commands.SequenceEqual(new[] { RelayNavigationCommand.Select }) && native.Count == 0,
                "actual Pause activation repeated, lost Resume or emitted a mapped desktop action.");
            report.Add("PASS held A invokes the actual Pause control once; its real ToggleControl handler keeps Relay paused and exposes Resume control. No mapped desktop input is sent.");
            await Press(Buttons.B);
            Check(ControlChangeSetup.IsKeyboardFocused && !service.Status.Enabled && native.Count == 0,
                "B did not reach the next actual control while paused.");
            await Press(Buttons.X);
            Check(PauseButton.IsKeyboardFocused && PauseButton.Content?.ToString() == "Resume control" && !service.Status.Enabled,
                "X could not return to the visible Resume action without a D-pad or chord.");
            await Press(Buttons.A, 25);
            Check(service.Status.Enabled && service.Status.Armed && PauseButton.Content?.ToString() == "Pause control" &&
                commands.SequenceEqual(new[] { RelayNavigationCommand.Select, RelayNavigationCommand.Next,
                    RelayNavigationCommand.Previous, RelayNavigationCommand.Select }) && native.Count == 0,
                "four switches failed to resume once or a held selection crossed the resume boundary.");
            report.Add("PASS paused B reaches Change my setup, X returns to actual Resume, held A invokes Resume once; the real service resumes only after neutral. Exactly four internal commands and no native output.");

            service.Toggle(); service.ProcessVerificationInput(Pad(), handle); RefreshStatus(service.Status);
            long generation = service.CaptureRelayNavigationGeneration(RelayNavigationCommand.Select);
            Check(service.CanNavigateRelayUi && !service.TryNavigateNativeDialog(RelayNavigationCommand.Select, handle, generation) && native.Count == 0,
                "paused WPF navigation acquired the native-key fallback.");
            report.Add("PASS paused navigation is eligible in the registered WPF main window; native-dialog routing still refuses a Select, even with a same-process HWND and current generation.");

            var beforeSource = new Window
            {
                Owner = this,
                Title = "Paused window registration preview",
                Width = 360,
                Height = 200,
                Content = new Button { Content = "Close preview" }
            };
            AccessibilitySupport.EnableKeyboardAccess(beforeSource);
            beforeSource.Show(); await Drain();
            IntPtr registered = new WindowInteropHelper(beforeSource).Handle;
            Check(service.OwnsPausedNavigationWindow(registered), "SourceInitialized did not register the configuration dialog.");
            service.ProcessVerificationInput(Pad(), registered);
            generation = service.CaptureRelayNavigationGeneration(RelayNavigationCommand.Select);
            beforeSource.Close(); await Drain();
            Check(!service.OwnsPausedNavigationWindow(registered) && !service.IsRelayNavigationCurrent(RelayNavigationCommand.Select, generation),
                "closing retained a stale HWND or queued action generation.");
            report.Add("PASS configuration window registration works before HWND creation; closing removes its exact HWND and invalidates queued navigation.");

            var afterSource = new Window
            {
                Owner = this,
                Title = "Late registration preview",
                Width = 360,
                Height = 200,
                Content = new Button { Content = "Close preview" }
            };
            afterSource.Show(); await Drain();
            IntPtr late = new WindowInteropHelper(afterSource).Handle;
            Check(!service.OwnsPausedNavigationWindow(late), "an arbitrary WPF window acquired navigation without opting in.");
            AccessibilitySupport.EnableKeyboardAccess(afterSource);
            Check(service.OwnsPausedNavigationWindow(late), "already-created opted-in WPF window was not registered.");
            afterSource.Close(); await Drain();
            Check(!service.OwnsPausedNavigationWindow(late), "late registration was not cleaned up.");
            report.Add("PASS an arbitrary WPF window stays ineligible until the access helper opts it in; already-created HWND registration and Closed cleanup work.");

            var keyboard = new KeyboardWindow(_ => true);
            keyboard.Show(); await Drain();
            IntPtr keyboardHandle = new WindowInteropHelper(keyboard).Handle;
            PausedRelayWindowNavigation.Attach(keyboard);
            Check(!service.OwnsPausedNavigationWindow(keyboardHandle), "the typing keyboard entered the configuration HWND allowlist.");
            int count = commands.Count;
            service.ProcessVerificationInput(Pad(), keyboardHandle);
            service.ProcessVerificationInput(Pad(Buttons.A, 32767), keyboardHandle); await Drain();
            Check(!service.CanNavigateRelayUi && commands.Count == count && !service.Status.Enabled && native.Count == 0,
                "paused typing keyboard or stick movement emitted output or internal configuration navigation.");
            keyboard.Close(); Activate(); await Drain();
            service.ProcessVerificationInput(Pad(Buttons.A), handle);
            Check(!service.Status.Armed && commands.Count == count, "returning from the typing window replayed held A.");
            service.ProcessVerificationInput(Pad(), handle);
            service.Suspend(true, false); service.ProcessVerificationInput(Pad(Buttons.B), handle); await Drain();
            Check(!service.CanNavigateRelayUi && commands.Count == count && native.Count == 0, "locked-session guard was bypassed.");
            service.Suspend(false, false); service.ProcessVerificationInput(Pad(), handle);
            service.SetAccessoryCapture(true); service.ProcessVerificationInput(Pad(Buttons.B), handle); await Drain();
            Check(!service.CanNavigateRelayUi && commands.Count == count && native.Count == 0, "accessory-capture guard was bypassed.");
            service.SetAccessoryCapture(false); service.ProcessVerificationInput(null, handle);
            service.ProcessVerificationInput(Pad(Buttons.A), handle);
            Check(!service.Status.Armed && commands.Count == count && native.Count == 0, "disconnect/reconnect replayed a held selection.");
            report.Add("PASS typing KeyboardWindow is excluded even on explicit attachment; paused inputs there send nothing. Returning requires neutral, and session-lock, accessory-capture and disconnect boundaries preserve quarantine.");
            Check(native.Count == 0 && external.Count == 0, "paused navigation emitted desktop input or opened Windows settings.");
            File.WriteAllLines(Path.Combine(output, "paused-controller-navigation.txt"), report.Append(
                "The real Pause/Resume button, WPF ownership registry, ControllerService and queued internal navigation are exercised with a fake output sink and synthetic controller frames. No hardware polling, native input injection or live Narrator acceptance is claimed."));
        }
        finally
        {
            _app.SetVerificationService(null); PausedRelayWindowNavigation.Detach(this);
            SelectPage(originalPage); RefreshStatus(); UpdateLayout();
            if (originalFocus is UIElement { IsVisible: true } element) element.Focus();
            else AccessibilitySupport.FocusHeading(PageHeading);
            await Drain();
        }
    }
}

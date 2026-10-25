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
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunProfileDeleteChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Profile deletion verification requires isolated preview mode without desktop output.");
        var originalProfiles = _app.Profiles;
        var originalSettings = _settings;
        int originalPage = Pages.SelectedIndex;
        double originalWidth = Width, originalHeight = Height;
        var report = new StringBuilder();
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Profile deletion: " + message);
        }
        ProfileLibrary Fixture(double scale = 1, bool longName = false) => new(1, "delete-fixture",
            [new("keep-fixture", "Kept fixture profile", originalSettings),
             new("delete-fixture", longName ? "A long profile name checked at twice the normal text size" : "Delete fixture profile",
                 originalSettings with { InterfaceScale = scale })])
        { SetupCompleted = true, AccessibilityRevision = 1 };
        void LoadFixture(double scale = 1, bool longName = false)
        {
            _app.Profiles = Fixture(scale, longName);
            _settings = _app.Profiles.Active.Settings;
            PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings();
            SelectPage(8); UpdateAccessibleViewport(); UpdateLayout();
        }
        IInvokeProvider Invoke(Button button)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
            Check(peer != null && !string.IsNullOrWhiteSpace(peer.GetName()) &&
                !string.IsNullOrWhiteSpace(peer.GetAutomationId()), "button has no meaningful automation name/identifier.");
            Check(button.ActualWidth >= 44 && button.ActualHeight >= 44, "button is smaller than 44 pixels.");
            return peer!.GetPattern(PatternInterface.Invoke) as IInvokeProvider ??
                throw new InvalidOperationException("Profile deletion button has no Invoke pattern.");
        }
        InputProcessor Controller(ProfileDeleteVerificationSink sink) => new(sink)
        {
            Settings = originalSettings with { NarratorNavigation = true, ButtonMappings = null, ButtonChords = null },
            RelayWindowActive = true,
            RelayNavigationRequested = command => Dispatcher.BeginInvoke(new Action(() => NavigateController(command)))
        };
        async Task Tick(InputProcessor processor, Buttons buttons)
        {
            processor.Process(new Gamepad(buttons, 0, 0, 0, 0, 0, 0), .008); await Drain();
        }
        async Task Run(string name, bool deleted, Func<ProfileDeleteConfirmation, Task> interact)
        {
            Exception? failure = null;
            bool observed = false;
            var completed = new TaskCompletionSource();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                ProfileDeleteConfirmation? dialog = null;
                try
                {
                    dialog = Application.Current.Windows.OfType<ProfileDeleteConfirmation>().Single(window => window.Owner == this && window.IsVisible);
                    observed = true;
                    await Drain(); dialog.UpdateLayout();
                    Check(dialog.Owner == this && dialog.IsEnabled, "confirmation is not an accessible owned window.");
                    Check(Keyboard.FocusedElement == dialog.CancelButton, "initial focus is not Cancel.");
                    Check(dialog.CancelButton.IsCancel && !dialog.DeleteButton.IsDefault, "default/cancel semantics are unsafe.");
                    Check(AutomationProperties.GetHeadingLevel(dialog.Heading) == AutomationHeadingLevel.Level1,
                        "confirmation heading has no heading metadata.");
                    Invoke(dialog.CancelButton); Invoke(dialog.DeleteButton);
                    await interact(dialog);
                }
                catch (Exception error) { failure = error; dialog?.Close(); }
                finally { completed.TrySetResult(); }
            }));
            DeleteProfile(this, new RoutedEventArgs());
            await completed.Task; await Drain();
            if (failure != null) throw new InvalidOperationException("Profile deletion fixture failed: " + name, failure);
            Check(observed, "confirmation did not open: " + name);
            Check(_app.Profiles.Profiles.Length == (deleted ? 1 : 2) &&
                _app.Profiles.ActiveId == (deleted ? "keep-fixture" : "delete-fixture"), "profile result disagrees with the chosen action: " + name);
            Check(_app.Profiles.Profiles.Any(profile => profile.Id == "keep-fixture"), "unrelated profile was removed.");
            report.AppendLine("PASS " + name + ": actual owned confirmation, safe initial focus, named 44-pixel Invoke controls and expected in-memory profile result.");
        }
        try
        {
            LoadFixture();
            await Run("Enter on initial Cancel preserves both profiles", false, async dialog =>
            {
                DispatchControlKey(dialog.CancelButton, Key.Enter); await Drain();
            });
            LoadFixture();
            await Run("controller B cancels through the real WPF navigation owner", false, async _ =>
            {
                var sink = new ProfileDeleteVerificationSink(); var processor = Controller(sink);
                await Tick(processor, Buttons.None); await Tick(processor, Buttons.Left); await Tick(processor, Buttons.None);
                await Tick(processor, Buttons.B);
                Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0, "controller cancellation also injected a native key.");
            });
            LoadFixture();
            await Run("UI Automation confirmation deletes only the named profile", true, async dialog =>
            {
                Invoke(dialog.DeleteButton).Invoke(); await Drain();
            });
            LoadFixture();
            await Run("controller Right then A confirms through the actual dialog", true, async dialog =>
            {
                var sink = new ProfileDeleteVerificationSink(); var processor = Controller(sink);
                await Tick(processor, Buttons.None); await Tick(processor, Buttons.Right); await Tick(processor, Buttons.None);
                Check(Keyboard.FocusedElement == dialog.DeleteButton, "one Right press did not focus Delete.");
                await Tick(processor, Buttons.A);
                Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0, "controller confirmation also injected a native key.");
            });
            LoadFixture();
            await Run("Enter only deletes after Delete receives focus", true, async dialog =>
            {
                dialog.Heading.Focus(); DispatchControlKey(dialog.Heading, Key.Enter); await Drain();
                Check(dialog.IsVisible && _app.Profiles.Profiles.Length == 2, "Enter on the heading acted as a destructive default.");
                Check(dialog.DeleteButton.Focus(), "Delete cannot receive keyboard focus.");
                DispatchControlKey(dialog.DeleteButton, Key.Enter); await Drain();
            });
            Width = 640; Height = 480; LoadFixture(2, true);
            await Run("200% text in a small window keeps Cancel and Delete reachable", false, async dialog =>
            {
                Check(dialog.Width <= 608 && dialog.ActualHeight <= 448.5, "large-text confirmation extends beyond its owner.");
                Check(dialog.CancelButton.FontSize >= 32 && dialog.DeleteButton.FontSize >= 32, "confirmation reduced user font scaling.");
                foreach (Button button in new[] { dialog.CancelButton, dialog.DeleteButton })
                {
                    var point = button.TranslatePoint(new Point(), dialog);
                    Check(point.X >= 0 && point.X + button.ActualWidth <= dialog.ActualWidth + .5 &&
                        point.Y >= 0 && point.Y + button.ActualHeight <= dialog.ActualHeight + .5,
                        "large-text action is clipped.");
                }
                Invoke(dialog.CancelButton).Invoke(); await Drain();
            });
            _app.Profiles = _app.Profiles with { Profiles = [_app.Profiles.Active] };
            PopulateProfiles(); DeleteProfile(this, new RoutedEventArgs());
            Check(_app.Profiles.Profiles.Length == 1 && !DeleteProfileButton.IsEnabled, "last-profile protection changed.");
            report.AppendLine("PASS final-profile protection. Only preview profiles were modified; no profile/settings files or native input were written. Live Narrator/controller use remains manual acceptance.");
            File.WriteAllText(Path.Combine(output, "profile-delete-confirmation.txt"), report.ToString());
        }
        finally
        {
            foreach (var dialog in Application.Current.Windows.OfType<ProfileDeleteConfirmation>().Where(window => window.Owner == this).ToArray()) dialog.Close();
            _app.Profiles = originalProfiles; _settings = originalSettings; Width = originalWidth; Height = originalHeight;
            PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); SelectPage(originalPage);
            UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
        }
    }
    private sealed class ProfileDeleteVerificationSink : IInputSink
    {
        internal readonly List<ushort[]> Keys = [];
        internal readonly List<Shortcut> Shortcuts = [];
        public void Move(int x, int y) { }
        public void Scroll(int vertical, int horizontal) { }
        public void SetMouseButton(ControllerMouse.Core.MouseButton button, bool down) { }
        public void Invoke(Shortcut shortcut) => Shortcuts.Add(shortcut);
        public void InvokeKeys(ushort[] keys) => Keys.Add((ushort[])keys.Clone());
    }
}

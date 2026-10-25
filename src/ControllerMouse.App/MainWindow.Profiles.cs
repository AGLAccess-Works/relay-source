using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ControllerMouse.Core;
using Microsoft.Win32;
using Control = ControllerMouse.Core.Control;
using MouseButton = ControllerMouse.Core.MouseButton;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private bool _profileLoading;
    private ControlBinding? _capturedBinding;
    private sealed record BindingOption(string Name, ControlBinding Binding);

    private void PopulateProfiles()
    {
        _profileLoading = true;
        try
        {
            ProfileCombo.ItemsSource = _app.Profiles.Profiles;
            ProfileCombo.SelectedValue = _app.Profiles.ActiveId;
            PopulateProfileRows();
            ProfileName.Text = _app.Profiles.Active.Name;
            DeleteProfileButton.IsEnabled = _app.Profiles.Profiles.Length > 1;
        }
        finally { _profileLoading = false; }
    }

    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileCombo.SelectedValue is string id) ActivateProfile(id);
    }
    private void ActivateProfile(string id)
    {
        if (_profileLoading || _loading || id == _app.Profiles.ActiveId || !_app.Profiles.Profiles.Any(profile => profile.Id == id)) return;
        _save.Stop();
        _app.Profiles = _app.Profiles.Update(_settings) with { ActiveId = id };
        _settings = _app.Profiles.Active.Settings.WithInterfacePreferences(_settings);
        PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); SaveSettings();
        ProfileNotice.Text = $"{_app.Profiles.Active.Name} is active. Release all controller controls to resume.";
    }

    private string UniqueName(string name)
    {
        var stem = name.Length > 50 ? name[..50] : name;
        string candidate = stem;
        for (int i = 2; _app.Profiles.Profiles.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); i++) candidate = $"{stem} ({i})";
        return candidate;
    }
    private bool AddProfile(Profile profile)
    {
        if (_app.Profiles.Profiles.Length >= 64) { ProfileNotice.Text = "You can save up to 64 profiles. Delete one before adding another."; return false; }
        _app.Profiles = _app.Profiles.Update(_settings);
        profile = profile with { Name = UniqueName(profile.Name) };
        _app.Profiles = _app.Profiles with { Profiles = [.. _app.Profiles.Profiles, profile], ActiveId = profile.Id };
        _settings = profile.Settings.WithInterfacePreferences(_settings);
        PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); SaveSettings();
        return true;
    }
    private void DuplicateProfile(object sender, RoutedEventArgs e)
    {
        if (!AddProfile(new(Guid.NewGuid().ToString("N"), _app.Profiles.Active.Name + " copy", _settings))) return;
        ProfileNotice.Text = "Copied the current settings and mappings. Rename your copy below.";
        ProfileName.Focus(); ProfileName.SelectAll();
    }
    private void RenameProfile(object sender, RoutedEventArgs e)
    {
        string name = ProfileName.Text.Trim();
        if (!ProfileLibrary.ValidName(name)) { ProfileNotice.Text = "Use a profile name of 1–60 characters."; return; }
        if (_app.Profiles.Profiles.Any(p => p.Id != _app.Profiles.ActiveId && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { ProfileNotice.Text = "Another profile already has that name."; return; }
        _app.Profiles = _app.Profiles with { Profiles = _app.Profiles.Profiles.Select(p => p.Id == _app.Profiles.ActiveId ? p with { Name = name } : p).ToArray() };
        PopulateProfiles(); SaveSettings(); ProfileNotice.Text = "Profile renamed.";
    }
    private void DeleteProfile(object sender, RoutedEventArgs e)
    {
        if (_app.Profiles.Profiles.Length <= 1) return;
        var profile = _app.Profiles.Active;
        if (!ProfileDeleteConfirmation.Confirm(this, profile.Name)) return;
        var remaining = _app.Profiles.Profiles.Where(p => p.Id != profile.Id).ToArray();
        _app.Profiles = _app.Profiles with { Profiles = remaining, ActiveId = remaining[0].Id };
        _settings = remaining[0].Settings.WithInterfacePreferences(_settings);
        PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings(); SaveSettings(); ProfileNotice.Text = "Profile deleted.";
    }
    private void ExportProfile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Relay profile (*.relay.json)|*.relay.json", FileName = "Relay-profile.relay.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { ProfileStore.Export(dialog.FileName, _app.Profiles.Active with { Settings = _settings }); ProfileNotice.Text = "Profile exported."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ProfileNotice.Text = "Could not export the profile: " + ex.Message; }
    }
    private void ImportProfile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Relay profile (*.relay.json)|*.relay.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { if (AddProfile(ProfileStore.Import(dialog.FileName))) ProfileNotice.Text = "Imported as a separate profile. Review its mappings before using it."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { ProfileNotice.Text = "Could not import the profile: " + ex.Message; }
    }

    private void InitializeMappingEditor()
    {
        MappingControl.ItemsSource = ControlMapping.Controls;
        MappingControl.SelectedIndex = 0;
        LoadBinding();
    }
    private void MappingControlChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadBinding();
        if (_presentationReady && !_syncingPresentationEditor && MappingControl.SelectedItem is ControlOption control && _selectedInput != control.Control.ToString())
            SelectPresentedInput(control.Control.ToString());
    }
    private void LoadBinding()
    {
        if (MappingControl?.SelectedItem is not ControlOption control || MappingAction == null) return;
        var options = new List<BindingOption> { new("No action", new()) };
        foreach (var button in Enum.GetValues<MouseButton>()) options.Add(new(button + " mouse button", new(BindingKind.Mouse, button)));
        if (control.Control is not (Control.Back or Control.Start))
        {
            options.Add(new("Hold for precision", new(BindingKind.Precision)));
            options.Add(new("Hold for speed boost", new(BindingKind.Boost)));
            foreach (var (name, key) in new[] { ("left", 37), ("up", 38), ("right", 39), ("down", 40) })
                options.Add(new("Move mouse pointer " + name, new(BindingKind.Pointer, Key: key)));
        }
        foreach (var action in AccessibilityCatalog.Actions.Where(a => a.Action != Shortcut.None &&
            (a.Action != Shortcut.LiveCaptions || Environment.OSVersion.Version.Build >= 22621)))
            options.Add(new(action.Name, new(BindingKind.Shortcut, Shortcut: action.Action)));
        foreach (var arrow in new[] { Shortcut.Up, Shortcut.Down, Shortcut.Left, Shortcut.Right })
            options.Add(new("Arrow " + arrow.ToString().ToLowerInvariant() + " (repeat)", new(BindingKind.Shortcut, Shortcut: arrow)));
        if (control.Control is not (Control.Back or Control.Start))
            foreach (var zoom in new[] { Shortcut.ZoomIn, Shortcut.ZoomOut })
                options.Add(new("Magnifier: " + (zoom == Shortcut.ZoomIn ? "zoom in" : "zoom out") + " (intentional hold repeat)",
                    new ControlBinding(BindingKind.Shortcut, Shortcut: zoom) { Repeat = true }));
        foreach (var (name, key) in new[] { ("Enter", 13), ("Escape", 27), ("Tab", 9), ("Space", 32), ("Backspace", 8), ("Page up", 33), ("Page down", 34), ("Home", 36), ("End", 35), ("Media play / pause", 179), ("Volume up", 175), ("Volume down", 174), ("Mute", 173) })
            options.Add(new(name + " (tap)", new(BindingKind.Keyboard, Key: key)));
        var current = ControlMapping.Resolve(_settings, control.Control);
        if (!options.Any(o => o.Binding == current)) options.Add(new(BindingName(current), current));
        MappingAction.ItemsSource = options;
        MappingAction.SelectedItem = options.First(o => o.Binding == current);
        MappingNotice.Text = control.Control is Control.Back or Control.Start
            ? "View and Menu act on release. Mouse actions are clicks here; hold modifiers are unavailable so the pause chord stays safe."
            : "Mouse actions stay held for dragging. Keyboard shortcuts send one tap. Arrow actions repeat while held.";
    }
    private void ApplyMapping(object sender, RoutedEventArgs e)
    {
        if (MappingControl.SelectedItem is not ControlOption control || MappingAction.SelectedItem is not BindingOption action) return;
        var mappings = _settings.ButtonMappings == null ? new Dictionary<Control, ControlBinding>() : new(_settings.ButtonMappings);
        mappings[control.Control] = action.Binding;
        _settings = _settings with { ButtonMappings = mappings };
        ReadSettings(); SaveSettings(); BuildGuide();
        MappingNotice.Text = $"{control.Name}: {BindingName(action.Binding)}. Release controls to resume.";
    }
    private void ResetDpadPointer(object sender, RoutedEventArgs e)
    {
        var mappings = _settings.ButtonMappings == null ? new Dictionary<Control, ControlBinding>() : new(_settings.ButtonMappings);
        foreach (var control in new[] { Control.Up, Control.Down, Control.Left, Control.Right })
            mappings[control] = ControlMapping.Default(new Settings(), control);
        _settings = _settings with { ButtonMappings = mappings };
        LoadBinding(); ReadSettings(); SaveSettings(); BuildGuide();
        MappingNotice.Text = "The D-pad now moves the mouse pointer. A clicks where the pointer is. Release the controls to continue.";
    }
    private void ResetMapping(object sender, RoutedEventArgs e)
    {
        if (MappingControl.SelectedItem is not ControlOption control) return;
        var mappings = _settings.ButtonMappings == null ? new Dictionary<Control, ControlBinding>() : new(_settings.ButtonMappings);
        mappings[control.Control] = ControlMapping.Default(new Settings(), control.Control);
        _settings = _settings with { ButtonMappings = mappings };
        LoadBinding(); ReadSettings(); SaveSettings(); BuildGuide();
    }
    private void CaptureShortcut(object sender, RoutedEventArgs e) => CaptureShortcut(sender, () => Keyboard.Modifiers);
    private void CaptureShortcut(object sender, Func<ModifierKeys> readModifiers,
        Action<Settings, bool>? verifyNavigationConfiguration = null)
    {
        if (verifyNavigationConfiguration != null && !_app.SmokeTest)
            throw new InvalidOperationException("Shortcut navigation substitution is limited to isolated verification.");
        _capturedBinding = null;
        var text = new TextBlock
        {
            Text = "Press the shortcut on a keyboard. Tab and Escape can also be recorded.\n" +
            "Your controller can still use this window: A selects, B moves forward, X moves back and Y cancels. " +
            "These controls also work while Relay is paused.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        text.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        AutomationProperties.SetAutomationId(text, "ShortcutRecorderInstructions");
        var capture = new TextBox { IsReadOnly = true, Text = "Press your shortcut here", TextWrapping = TextWrapping.Wrap };
        capture.SetResourceReference(MinHeightProperty, "ControlMinHeight");
        AccessibilitySupport.CaptureTabInput(capture);
        AutomationProperties.SetName(capture, "Shortcut recorder");
        AutomationProperties.SetHelpText(capture, "Press the key or keys on a keyboard to record. Tab and Escape can also be recorded. After recording, focus moves to Use shortcut. Controller B reaches Cancel; X moves back and Y cancels. Controller selection never records a key.");
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(text); panel.Children.Add(capture);
        var buttons = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        var use = new Button { Content = "Use shortcut", IsEnabled = false, IsDefault = true, Margin = new Thickness(0, 0, 12, 8) };
        var cancel = new Button { Content = "Cancel", IsCancel = true }; buttons.Children.Add(use); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        var scroll = new ScrollViewer
        {
            Content = panel,
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetAutomationId(scroll, "ShortcutRecorderViewport");
        var dialog = new Window
        {
            Owner = this,
            Title = "Record keyboard shortcut",
            Width = 510,
            MinWidth = 360,
            MinHeight = 260,
            MaxWidth = Math.Max(360, Math.Min(ActualWidth - 32, SystemParameters.WorkArea.Width - 32)),
            MaxHeight = Math.Max(260, Math.Min(ActualHeight - 32, SystemParameters.WorkArea.Height - 32)),
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize,
            Content = scroll
        };
        dialog.SetResourceReference(FontSizeProperty, "MainFontSize");
        dialog.SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
        dialog.SetResourceReference(BackgroundProperty, "PanelBrush"); dialog.SetResourceReference(ForegroundProperty, "TextBrush");
        AccessibilitySupport.EnableKeyboardAccess(dialog);
        AutomationProperties.SetIsDialog(dialog, true);
        capture.PreviewKeyDown += (_, keyEvent) =>
        {
            keyEvent.Handled = true;
            var key = keyEvent.Key == Key.System ? keyEvent.SystemKey : keyEvent.Key;
            var pressedModifiers = readModifiers();
            int modifiers = (pressedModifiers.HasFlag(ModifierKeys.Control) ? 1 : 0) | (pressedModifiers.HasFlag(ModifierKeys.Alt) ? 2 : 0) |
                (pressedModifiers.HasFlag(ModifierKeys.Shift) ? 4 : 0) | (pressedModifiers.HasFlag(ModifierKeys.Windows) ? 8 : 0);
            var binding = new ControlBinding(BindingKind.Keyboard, Key: KeyInterop.VirtualKeyFromKey(key), Modifiers: modifiers);
            if (!binding.IsValid) return;
            _capturedBinding = binding; capture.Text = BindingName(binding); use.IsEnabled = true;
            AutomationProperties.SetName(use, "Use " + BindingName(binding) + " shortcut");
            // Leave the recorder after a complete key so Tab and Escape are both recordable.
            capture.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        };
        use.Click += (_, _) => dialog.DialogResult = true;
        cancel.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) => capture.Focus();
        bool wasConfigurationMode = _app.Service?.ConfigurationMode == true;
        void ConfigureNavigation(Settings settings, bool configurationMode)
        {
            // The temporary layout belongs only to Relay's owned windows. It
            // never enables a paused session or injects the shortcut being recorded.
            _app.Service?.UpdateSettings(settings, _app.Profiles.ActiveId);
            _app.Service?.SetConfigurationMode(configurationMode);
            verifyNavigationConfiguration?.Invoke(settings, configurationMode);
        }
        try
        {
            ConfigureNavigation(SetupPlan.Navigation(_settings, SetupInputMethod.Switches), true);
            if (dialog.ShowDialog() == true && _capturedBinding != null)
            {
                if (sender is FrameworkElement { Tag: "Presentation" }) { SetPresentedBinding(_capturedBinding); return; }
                bool forChord = sender is FrameworkElement { Tag: "Chord" };
                var destination = forChord ? ChordAction : MappingAction;
                var options = ((IEnumerable<BindingOption>)destination.ItemsSource).ToList();
                var option = new BindingOption(BindingName(_capturedBinding), _capturedBinding);
                options.Add(option); destination.ItemsSource = options; destination.SelectedItem = option;
                if (forChord) ChordNotice.Text = "Shortcut captured. Choose Save combination to apply it.";
                else MappingNotice.Text = "Shortcut captured. Choose Apply assignment to save it.";
            }
        }
        finally
        {
            ConfigureNavigation(_settings, wasConfigurationMode);
            if (sender is System.Windows.Controls.Control source && source.IsVisible) source.Focus();
        }
    }
    private string BindingName(ControlBinding binding) => (binding.Kind switch
    {
        BindingKind.Mouse => binding.Mouse == MouseButton.Left ? "Left click / drag" : binding.Mouse + " click",
        BindingKind.Shortcut => ActionName(binding.Shortcut),
        BindingKind.Precision => "Precision",
        BindingKind.Boost => "Speed boost",
        BindingKind.Pointer => "Move pointer " + (binding.Key switch { 37 => "left", 38 => "up", 39 => "right", _ => "down" }),
        BindingKind.Keyboard => ((binding.Modifiers & 1) != 0 ? "Ctrl+" : "") + ((binding.Modifiers & 2) != 0 ? "Alt+" : "") +
            ((binding.Modifiers & 4) != 0 ? "Shift+" : "") + ((binding.Modifiers & 8) != 0 ? "Win+" : "") + KeyInterop.KeyFromVirtualKey(binding.Key) + " (tap)",
        _ => "No action"
    }) + (binding.Repeat ? " (hold repeat)" : "");

    private void RunProfileSmokeChecks()
    {
        string original = _app.Profiles.ActiveId;
        MappingControl.SelectedItem = ControlMapping.Controls.First(c => c.Control == Control.Y);
        MappingAction.SelectedItem = ((IEnumerable<BindingOption>)MappingAction.ItemsSource).First(o => o.Binding.Shortcut == Shortcut.Narrator);
        ApplyMapping(this, new RoutedEventArgs());
        if (ControlMapping.Resolve(_settings, Control.Y).Shortcut != Shortcut.Narrator) throw new InvalidOperationException("Mapping editor failed.");
        DuplicateProfile(this, new RoutedEventArgs());
        string copy = _app.Profiles.ActiveId;
        ProfileName.Text = "Smoke test profile"; RenameProfile(this, new RoutedEventArgs());
        SpeedSlider.Value = 1234;
        ProfileCombo.SelectedValue = original;
        if (_settings.PointerSpeed == 1234) throw new InvalidOperationException("Profiles leaked settings.");
        ProfileCombo.SelectedValue = copy;
        if (_settings.PointerSpeed != 1234 || _app.Profiles.Active.Name != "Smoke test profile") throw new InvalidOperationException("Profile switch failed.");
        ResetMapping(this, new RoutedEventArgs());
        if (ControlMapping.Resolve(_settings, Control.Y).Shortcut != Shortcut.CloseMagnifier) throw new InvalidOperationException("Mapping reset failed.");
        _app.Profiles = ProfileLibrary.Create(new Settings());
        _settings = _app.Profiles.Active.Settings; PopulateProfiles(); PopulateControls(); LoadBinding(); ReadSettings();
    }
}

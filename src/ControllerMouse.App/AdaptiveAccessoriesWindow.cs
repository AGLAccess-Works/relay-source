using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;
using Control = System.Windows.Controls.Control;

namespace ControllerMouse.App;

/// <summary>An isolated working copy: one final Save keeps every accessory; Cancel keeps none of the edits.</summary>
internal sealed class AdaptiveAccessoriesWindow : Window
{
    private readonly List<AdaptiveAccessory> _accessories;
    private readonly Func<Gamepad?> _read;
    private readonly Action<bool> _captureMode;
    private readonly bool _identifyFirst;
    private readonly ListBox _list = new() { MinHeight = 110 };
    private readonly ComboBox _port = new() { ItemsSource = AdaptiveAccessory.Ports, DisplayMemberPath = "Name", SelectedIndex = 0 };
    private readonly ComboBox _kind = new() { ItemsSource = Enum.GetValues<AccessoryKind>(), SelectedIndex = 0 };
    private readonly TextBox _name = new() { MaxLength = 60, MinHeight = 44 };
    private readonly TextBlock _inputs = new() { TextWrapping = TextWrapping.Wrap }, _notice = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly AccessoryCapture _capture = new();
    private readonly Button _add, _remove, _record, _stop, _save, _cancel;
    private AccessorySignal[] _signals = [];
    private bool _recording, _loadingEditor, _updatingList, _dirty, _identifying;
    private string? _editingPort;
    private DateTime _deadline;
    internal AdaptiveAccessory[]? Result { get; private set; }

    internal AdaptiveAccessoriesWindow(AdaptiveAccessory[]? accessories, Func<Gamepad?> read, Action<bool> captureMode, bool identifyFirst = false)
    {
        _accessories = AdaptiveAccessory.Validate(accessories).ToList(); _read = read; _captureMode = captureMode; _identifyFirst = identifyFirst;
        Title = identifyFirst ? "Identify and name an input" : "My Adaptive Controller accessories"; Width = 720; Height = 760; MinWidth = 480; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; SetResourceReference(FontSizeProperty, "MainFontSize");
        AccessibilitySupport.EnableKeyboardAccess(this);
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        AutomationProperties.SetName(this, Title);
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(Heading(identifyFirst ? "Identify an input" : "My accessories", "PageTitleFontSize", AutomationHeadingLevel.Level1));
        body.Children.Add(new TextBlock
        {
            Text = identifyFirst ? "Let go of all controls. Then press the switch or move the control you want to name. Relay will tell you which Xbox input it sends." :
                "Give each switch, pedal or joystick a name. Choose its socket, then record a press or movement if you want to.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 8)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Choose Save and close once to keep everything. Cancel leaves your profile as it was.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
        _list.SetResourceReference(BackgroundProperty, "ControlBrush"); _list.SetResourceReference(ForegroundProperty, "TextBrush");
        Accessible(_list, "Accessories in this setup", "AccessoryList", "Use Up and Down to choose an accessory to edit.");
        body.Children.Add(_list);
        _add = MakeButton("Add another accessory", () => { if (KeepCurrentDetails()) { NewAccessory(); if (_identifyFirst) StartRecord(); } });
        _remove = MakeButton("Remove selected accessory", RemoveSelected);
        var manage = new WrapPanel(); manage.Children.Add(_add); manage.Children.Add(_remove); body.Children.Add(manage);
        if (identifyFirst)
        {
            Field(body, "1. Name the detected input", _name, "AccessoryName", "For example, green switch or left foot pedal. Relay will move here after detecting an input.");
            Field(body, "2. Choose its actual socket", _port, "AccessorySocket", "Choose where you plugged this accessory in. Relay cannot infer its physical socket from an Xbox input.");
            Field(body, "3. Choose the type", _kind, "AccessoryType", "Switch, button, pedal, joystick, or other. This is your description, not an automatically detected device type.");
        }
        else
        {
            Field(body, "1. Choose the socket", _port, "AccessorySocket", "Choose the socket or USB port printed on your Adaptive Controller.");
            Field(body, "2. Choose the type", _kind, "AccessoryType", "Switch, button, pedal, joystick, or other.");
            Field(body, "3. Give it a name", _name, "AccessoryName", "For example, left foot pedal. A name helps you recognise your accessory.");
        }
        body.Children.Add(Heading(identifyFirst ? "Detected input" : "4. Record its input (optional)", "SectionFontSize", AutomationHeadingLevel.Level2));
        body.Children.Add(new TextBlock
        {
            Text = "Choose Record input. Let go of all controls. Then press or move only this accessory. For a joystick with several buttons, record each button in turn.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });
        AutomationProperties.SetAutomationId(_inputs, "AccessoryRecordedInputs"); body.Children.Add(_inputs);
        _inputs.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        _record = MakeButton("Record input", StartRecord); _stop = MakeButton("Stop recording", () =>
        {
            StopRecord(); _notice.Text = "Recording stopped. You can keep editing or save your setup.";
        });
        var recording = new WrapPanel(); recording.Children.Add(_record); recording.Children.Add(_stop); body.Children.Add(recording);
        body.Children.Add(new TextBlock
        {
            Text = "This is a setup you record. Windows sends combined Xbox inputs, so Relay cannot identify each accessory or tell when it is unplugged.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 12)
        });
        AutomationProperties.SetAutomationId(_notice, "AccessorySetupStatus");
        _notice.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        AccessibilitySupport.AnnounceChanges(_notice); body.Children.Add(_notice);
        _save = MakeButton("Save and close", SaveAndClose); _save.IsDefault = true;
        _cancel = MakeButton("Cancel", Close); _cancel.IsCancel = true;
        var finish = new WrapPanel(); finish.Children.Add(_save); finish.Children.Add(_cancel); body.Children.Add(finish);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        KeyboardNavigation.SetTabNavigation(scroll, KeyboardNavigationMode.Cycle); Content = scroll;
        _list.SelectionChanged += (_, _) =>
        {
            if (_updatingList || _list.SelectedIndex < 0) return;
            string selectedPort = _accessories[_list.SelectedIndex].Port;
            StopRecord();
            if (!KeepCurrentDetails()) { RefreshList(_editingPort); return; }
            var item = _accessories.FirstOrDefault(a => a.Port == selectedPort);
            if (item == null) { NewAccessory(); return; }
            LoadAccessory(item); RefreshList(selectedPort); _notice.Text = "Editing " + item.Name + ".";
        };
        _port.SelectionChanged += (_, _) => MarkDirty();
        _kind.SelectionChanged += (_, _) => MarkDirty();
        _name.TextChanged += (_, _) => MarkDirty();
        _timer.Tick += (_, _) =>
        {
            if (!_recording) return;
            if (DateTime.UtcNow >= _deadline)
            {
                StopRecord(); _notice.Text = "No input recorded. Choose Record input to try again. You can also save without recording."; return;
            }
            var input = _read();
            if (input == null)
            {
                StopRecord(); _notice.Text = "No controller is available. Connect or resume your controller, then choose Record input again."; return;
            }
            var signal = _capture.Observe(input);
            if (signal is { } found)
            {
                _signals = [.. _signals.Append(found).Distinct()]; _dirty = true;
                StopRecord(); UpdateInputs();
                if (_identifying)
                {
                    _identifying = false; _name.Focus();
                    _notice.Text = "Input detected: " + AdaptiveAccessory.SignalName(found) + ". Give it a name, then choose where you plugged it in. The input does not identify a physical socket.";
                }
                else _notice.Text = "Recorded " + AdaptiveAccessory.SignalName(found) + ". You can record another input or choose Save and close.";
            }
            else _notice.Text = _capture.Armed ? "Ready. Press or move only this accessory." : "Let go of all controls first.";
        };
        PreviewKeyDown += (_, e) =>
        {
            if (_recording && e.Key == Key.Escape)
            {
                StopRecord(); _notice.Text = "Recording stopped. Press Escape again to cancel your setup."; e.Handled = true;
            }
            else if (_recording && e.Key == Key.Enter && !_stop.IsKeyboardFocused && !_cancel.IsKeyboardFocused)
            {
                _notice.Text = "Recording is still running. Use your accessory, or choose Stop recording first."; e.Handled = true;
            }
        };
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => { if (_identifyFirst) StartRecord(); else _port.Focus(); }, DispatcherPriority.Input);
        Closed += (_, _) => StopRecord();
        RefreshList(); NewAccessory(false);
    }

    private static TextBlock Heading(string text, string sizeResource, AutomationHeadingLevel level)
    {
        var heading = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };
        heading.SetResourceReference(TextBlock.FontSizeProperty, sizeResource);
        AutomationProperties.SetHeadingLevel(heading, level); return heading;
    }
    private static void Accessible(DependencyObject control, string name, string id, string help = "")
    {
        AutomationProperties.SetName(control, name); AutomationProperties.SetAutomationId(control, id);
        if (help.Length > 0) AutomationProperties.SetHelpText(control, help);
    }
    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 8, 10, 8) };
        button.SetResourceReference(Control.FontSizeProperty, "MainFontSize");
        button.SetResourceReference(Control.MinHeightProperty, "ControlMinHeight");
        Accessible(button, text, "Accessory" + new string(text.Where(char.IsLetterOrDigit).ToArray()));
        button.Click += (_, _) => action(); return button;
    }
    private static void Field(Panel body, string text, Control control, string id, string help)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 12, 0, 6) };
        body.Children.Add(label); Accessible(control, text, id, help); AutomationProperties.SetLabeledBy(control, label);
        control.SetResourceReference(Control.MinHeightProperty, "ControlMinHeight");
        control.SetResourceReference(Control.FontSizeProperty, "MainFontSize"); body.Children.Add(control);
    }
    private void MarkDirty() { if (!_loadingEditor) _dirty = true; }
    private void RefreshList(string? selectedPort = null)
    {
        _updatingList = true;
        try
        {
            _list.ItemsSource = _accessories.Select(a => a.Name + " — " + AdaptiveAccessory.Ports.First(p => p.Id == a.Port).Name).ToArray();
            _list.SelectedIndex = selectedPort == null ? -1 : _accessories.FindIndex(a => a.Port == selectedPort);
        }
        finally { _updatingList = false; }
        UpdateActions();
    }
    private void NewAccessory(bool focus = true)
    {
        StopRecord(); _loadingEditor = true;
        try
        {
            _editingPort = null; _name.Text = ""; _signals = []; _kind.SelectedItem = AccessoryKind.Switch;
            _port.SelectedItem = _identifyFirst ? null : AdaptiveAccessory.Ports.FirstOrDefault(p => !_accessories.Any(a => a.Port == p.Id)) ?? AdaptiveAccessory.Ports[0];
            _dirty = false; RefreshList(); UpdateInputs();
        }
        finally { _loadingEditor = false; }
        if (focus) { _notice.Text = "New accessory. Choose its socket, type and name."; _port.Focus(); }
    }
    private void LoadAccessory(AdaptiveAccessory item)
    {
        _loadingEditor = true;
        try
        {
            _editingPort = item.Port; _port.SelectedItem = AdaptiveAccessory.Ports.First(p => p.Id == item.Port);
            _kind.SelectedItem = item.Kind; _name.Text = item.Name; _signals = item.Signals.ToArray(); _dirty = false; UpdateInputs();
        }
        finally { _loadingEditor = false; }
    }
    private bool KeepCurrentDetails()
    {
        StopRecord();
        if (!_dirty && _editingPort == null && string.IsNullOrWhiteSpace(_name.Text) && _signals.Length == 0) return true;
        if (_identifyFirst && string.IsNullOrWhiteSpace(_name.Text))
        {
            _notice.Text = "Give the detected input a name, for example green switch."; _name.Focus(); return false;
        }
        if (_port.SelectedItem is not AccessoryPort port || _kind.SelectedItem is not AccessoryKind kind)
        {
            _notice.Text = "Choose the socket where you plugged this accessory in. Relay cannot determine its physical socket from the input.";
            _port.Focus(); return false;
        }
        var occupied = _accessories.FirstOrDefault(a => a.Port == port.Id && a.Port != _editingPort);
        if (occupied != null)
        {
            _notice.Text = port.Name + " is already used by " + occupied.Name + ". Choose a different socket, or edit that accessory in the list.";
            _port.Focus(); return false;
        }
        var item = AdaptiveAccessory.Validate([new(port.Id, _name.Text, kind, _signals)])[0];
        // A socket holds one accessory. Changing a socket edits that entry rather than leaving a stale duplicate.
        if (_editingPort != null && _editingPort != port.Id) _accessories.RemoveAll(a => a.Port == _editingPort);
        int index = _accessories.FindIndex(a => a.Port == port.Id);
        if (index >= 0) _accessories[index] = item; else _accessories.Add(item);
        _editingPort = port.Id; _dirty = false;
        RefreshList(port.Id);
        return true;
    }
    private void RemoveSelected()
    {
        if (_list.SelectedIndex < 0) return;
        var item = _accessories[_list.SelectedIndex];
        _accessories.RemoveAt(_list.SelectedIndex); NewAccessory();
        _notice.Text = "Removed " + item.Name + ". Choose Save and close to keep this change.";
    }
    private void SaveAndClose()
    {
        if (_recording) { _notice.Text = "Stop recording before saving."; return; }
        if (!KeepCurrentDetails()) return;
        Result = AdaptiveAccessory.Validate(_accessories); Close();
    }
    private void UpdateInputs() => _inputs.Text = _signals.Length == 0
        ? "No input recorded. Recording is optional."
        : "Recorded: " + string.Join(", ", _signals.Select(AdaptiveAccessory.SignalName));
    private void UpdateActions()
    {
        _add.IsEnabled = _save.IsEnabled = !_recording;
        _remove.IsEnabled = !_recording && _list.SelectedIndex >= 0;
        _record.IsEnabled = !_recording; _stop.IsEnabled = _recording;
        _port.IsEnabled = _kind.IsEnabled = _name.IsEnabled = !_recording;
    }
    private void StartRecord()
    {
        StopRecord(); _capture.Observe(null); _captureMode(true); _recording = true;
        _identifying = _identifyFirst && _signals.Length == 0;
        _deadline = DateTime.UtcNow.AddSeconds(20); UpdateActions(); _timer.Start();
        if (IsVisible) _stop.Focus();
        _notice.Text = "Let go of all controls. Then use only this accessory. Relay pauses desktop input while recording. You have 20 seconds.";
    }
    private void StopRecord()
    {
        _timer.Stop(); bool wasRecording = _recording; _recording = false;
        if (wasRecording) _captureMode(false);
        UpdateActions();
        if (wasRecording && IsVisible) _record.Focus();
    }

    internal async System.Threading.Tasks.Task VerifyForTest(string output, Action<Gamepad> setPad)
    {
        _name.Text = "Left foot pedal"; _port.SelectedItem = AdaptiveAccessory.Ports.First(p => p.Id == "A");
        _kind.SelectedItem = AccessoryKind.Pedal; setPad(default); StartRecord();
        if (_save.IsEnabled || _add.IsEnabled || _record.IsEnabled || !_stop.IsEnabled)
            throw new InvalidOperationException("Recording did not quarantine editing and final save.");
        var stopEnter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.Enter)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        _stop.RaiseEvent(stopEnter);
        if (stopEnter.Handled) throw new InvalidOperationException("Recording consumed Enter before the Stop recording button could activate.");
        await System.Threading.Tasks.Task.Delay(100);
        setPad(new(Buttons.A, 0, 0, 0, 0, 0, 0)); await System.Threading.Tasks.Task.Delay(100);
        if (_recording || !_signals.SequenceEqual(new[] { AccessorySignal.A }) || !_save.IsEnabled)
            throw new InvalidOperationException("Accessory recording failed or failed to release capture mode.");
        setPad(default);
        foreach (var control in Descendants(this).OfType<Control>().Where(c => c is Button or ComboBox or TextBox or ListBox))
        {
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(control);
            var pattern = control is Button ? System.Windows.Automation.Peers.PatternInterface.Invoke : control is TextBox
                ? System.Windows.Automation.Peers.PatternInterface.Value : System.Windows.Automation.Peers.PatternInterface.Selection;
            if (peer == null || string.IsNullOrWhiteSpace(peer.GetName()) || string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(control)) || peer.GetPattern(pattern) == null)
                throw new InvalidOperationException("Accessory control lacks an accessible name, stable identifier or action.");
        }
        if (!_save.IsDefault || !_cancel.IsCancel || AutomationProperties.GetHeadingLevel(Descendants(this).OfType<TextBlock>().First()) != AutomationHeadingLevel.Level1)
            throw new InvalidOperationException("Accessory dialog lacks default, cancel or heading semantics.");
        Resources["MainFontSize"] = 32d; Resources["CaptionFontSize"] = 28d; Resources["PageTitleFontSize"] = 56d;
        Resources["SectionFontSize"] = 40d; Resources["ControlMinHeight"] = 88d;
        Width = 480; Height = 480; await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle);
        var viewport = (ScrollViewer)Content;
        if (_name.FontSize != 32 || _port.FontSize != 32 || viewport.ScrollableHeight <= 0)
            throw new InvalidOperationException("The accessory dialog did not use 200% interface-size resources and a scrollable small viewport.");
        foreach (var control in new Control[] { _name, _port, _record, _save, _cancel })
        {
            control.BringIntoView(); await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle);
            var bounds = control.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), control.RenderSize));
            if (control.ActualWidth < 44 || control.ActualHeight < 44 || bounds.Left < -1 || bounds.Right > viewport.ActualWidth + 1 ||
                !bounds.IntersectsWith(new Rect(new Point(), viewport.RenderSize)))
                throw new InvalidOperationException("An accessory setup action was clipped or unreachable at 200% in a 480x480 viewport.");
        }
        viewport.ScrollToTop(); await Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "adaptive-accessory-setup.png"))) encoder.Save(file);
        // The final Save commits the visible entry, with no separate per-accessory Save.
        _save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if (Result?.Length != 1 || Result[0].Name != "Left foot pedal" || !Result[0].Signals.SequenceEqual([AccessorySignal.A]))
            throw new InvalidOperationException("One-step final Save did not retain the current accessory.");
        var original = new AdaptiveAccessory("A", "Original switch", AccessoryKind.Switch, [AccessorySignal.A]);
        var cancel = new AdaptiveAccessoriesWindow([original], () => null, _ => { });
        cancel.LoadAccessory(original); cancel._name.Text = "Changed switch";
        cancel._cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if (cancel.Result != null || original.Name != "Original switch") throw new InvalidOperationException("Cancel retained accessory edits.");
        var editing = new AdaptiveAccessoriesWindow(null, () => null, _ => { });
        editing._name.Text = "First switch"; editing._port.SelectedItem = AdaptiveAccessory.Ports.First(p => p.Id == "A");
        editing._add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        editing._name.Text = "Second switch"; editing._port.SelectedItem = AdaptiveAccessory.Ports.First(p => p.Id == "B");
        editing._save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if (editing.Result?.Length != 2 || !editing.Result.Select(a => a.Name).SequenceEqual(["First switch", "Second switch"]))
            throw new InvalidOperationException("Adding another accessory lost unsaved details.");
        Gamepad identifiedPad = new(Buttons.B, 0, 0, 0, 0, 0, 0);
        var identifyStates = new List<bool>();
        var identify = new AdaptiveAccessoriesWindow(null, () => identifiedPad, capturing => identifyStates.Add(capturing), true)
        { Owner = Owner, Topmost = true };
        try
        {
            identify.Show(); await System.Threading.Tasks.Task.Delay(100);
            if (!identify._recording || identify._signals.Length != 0 || identify._port.SelectedItem != null)
                throw new InvalidOperationException("Identify inferred a port or recorded a control held before opening.");
            identifiedPad = default; await System.Threading.Tasks.Task.Delay(100);
            identifiedPad = new(Buttons.B, 0, 0, 0, 0, 0, 0); await System.Threading.Tasks.Task.Delay(100);
            if (identify._recording || !identify._signals.SequenceEqual([AccessorySignal.B]) || !identify._name.IsKeyboardFocused || identify._port.SelectedItem != null)
                throw new InvalidOperationException("Identify did not detect one logical input and focus its name without inventing a socket.");
            var noticePeer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(identify._notice);
            if (noticePeer?.GetName().Contains("Input detected:") != true)
                throw new InvalidOperationException("The detected input is not exposed in the spoken status text.");
            identifiedPad = default; identify._name.Text = "Green switch";
            identify._save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if (identify.Result != null || !identify.IsVisible || !identify._notice.Text.Contains("cannot determine"))
                throw new InvalidOperationException("Identify saved an unconfirmed physical socket.");
            identify._port.SelectedItem = AdaptiveAccessory.Ports.First(port => port.Id == "A");
            identify._save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if (identify.Result is not [{ Name: "Green switch", Port: "A" }] || !identify.Result[0].Signals.SequenceEqual([AccessorySignal.B]))
                throw new InvalidOperationException("Identify lost the name, user-confirmed socket or detected logical signal.");
            if (!identifyStates.SequenceEqual([true, false])) throw new InvalidOperationException("Identify did not bracket capture safely.");
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, "adaptive-identify.txt"),
                "PASS: identify-first entry needs no preselected socket; held input rejected until neutral; one simulated logical input detected; name receives focus; detected status available through UI Automation; no physical socket inferred; final Save requires the user-confirmed socket and retains name/signal; capture safely bracketed. No native input sent or physical accessory identity claimed.");
        }
        finally { identify.Close(); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child; foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

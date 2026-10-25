using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ControllerMouse.Core;
using Control = ControllerMouse.Core.Control;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private string _selectedInput = "A", _actionCategory = "Pointer & mouse", _deviceSignature = "";
    private bool _presentationReady, _portsView, _devicesLoading, _syncingPresentationEditor;
    private readonly Dictionary<string, InputPresentation> _presentedInputs = new();
    private sealed record InputTile(Button Button, TextBlock Name, TextBlock Action, TextBlock State, ControllerInputIcon Icon, Border Badge);
    private sealed record InputInspector(TextBlock Name, TextBlock Action, TextBlock Category, TextBlock State, Border Badge, TextBlock Details, Button Change, Button Clear, ControllerInputIcon Icon, TextBlock StateValue, TextBlock Repeat);
    private readonly Dictionary<string, InputTile> _controlTiles = new(), _hardwareTiles = new();
    private readonly List<InputInspector> _inspectors = [];
    private ComboBox? _hardwareDevices, _hardwareLayout;
    private TextBlock? _hardwareInfo, _hardwareNotice, _inputAnnouncement;
    private readonly List<Button> _frontButtons = [], _portButtons = [];
    private string _tileKind = "", _lastAnnouncedInput = "";
    private ControllerStatus? _lastPresentationStatus;
    private Settings? _lastPresentationSettings;
    private (bool Adaptive, int Page, bool Navigation, bool Keyboard) _lastPresentationContext;
    private DateTime _nextDeviceRefresh;
    private DateTime _reconnectAt;
    private bool _reconnectPending;

    private TextBlock PresentationText(string text, string size = "MainFontSize", string colour = "TextBrush")
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        label.SetResourceReference(TextBlock.FontSizeProperty, size);
        label.SetResourceReference(TextBlock.ForegroundProperty, colour);
        return label;
    }
    private Button PresentationButton(string text, string id, Action action)
    {
        var button = new Button { Content = ButtonText(text), Margin = new Thickness(0, 4, 8, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(button, text); AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => action();
        return button;
    }
    private TextBlock ButtonText(string text)
    {
        var label = PresentationText(text); label.ClearValue(TextBlock.ForegroundProperty); label.Margin = new Thickness(0); return label;
    }
    private Control? SelectedMappedControl()
    {
        if (_portsView && Pages.SelectedIndex != 6)
        {
            var accessory = (_settings.AdaptiveAccessories ?? []).FirstOrDefault(accessory => accessory.Port == _selectedInput);
            return accessory?.Signals.Length == 1 && (int)accessory.Signals[0] < 16 ? (Control)accessory.Signals[0] : null;
        }
        return Enum.TryParse<Control>(_selectedInput, out var control) ? control : null;
    }
    private void InitializePresentation()
    {
        foreach (var surface in new[] { LiveSurface, MappingSurface, HardwareSurface })
        {
            surface.InputSelected += SelectPresentedInput;
            surface.SelectedInput = _selectedInput;
        }
        BuildInspector(ControlInspectorHost, "Control");
        BuildInspector(MappingInspectorHost, "Mapping");
        BuildInspector(HardwareInspectorHost, "Hardware");
        foreach (var host in new[] { ControlViewsHost, HardwareViewsHost })
        {
            var views = new WrapPanel();
            var front = PresentationButton("Front view", "FrontView_" + host.Name, () => SetPortsView(false));
            var ports = PresentationButton("Ports view", "PortsView_" + host.Name, () => SetPortsView(true));
            _frontButtons.Add(front); _portButtons.Add(ports);
            views.Children.Add(front); views.Children.Add(ports); host.Children.Add(views);
        }
        _hardwareDevices = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Slot", IsEnabled = false, Margin = new Thickness(0, 0, 0, 12) };
        AutomationProperties.SetName(_hardwareDevices, "Controller to use"); AutomationProperties.SetAutomationId(_hardwareDevices, "HardwareController");
        AutomationProperties.SetHelpText(_hardwareDevices, "Select one connected controller. Release its controls before using it.");
        _hardwareDevices.SelectionChanged += (_, _) =>
        {
            if (_devicesLoading || _hardwareDevices.SelectedValue is not int slot) return;
            bool selected = _app.Service?.SelectController(slot) == true;
            if (_hardwareNotice != null) _hardwareNotice.Text = selected ? "Controller selected. Release all controls to continue." : "This controller is no longer available. Choose Reconnect.";
        };
        HardwareDeviceHost.Children.Add(PresentationText("Controller to use", "SectionFontSize"));
        HardwareDeviceHost.Children.Add(_hardwareDevices);
        _hardwareLayout = new ComboBox { ItemsSource = new[] { "Automatic identification", "Xbox controller", "Xbox Adaptive Controller" }, SelectedIndex = (int)_settings.ControllerType, Margin = new Thickness(0, 0, 0, 12) };
        AutomationProperties.SetName(_hardwareLayout, "Controller layout"); AutomationProperties.SetAutomationId(_hardwareLayout, "HardwareLayout");
        AutomationProperties.SetHelpText(_hardwareLayout, "Choose Adaptive if Windows cannot identify it. A chosen layout does not detect attached accessories.");
        _hardwareLayout.SelectionChanged += (_, _) => { if (!_devicesLoading && _hardwareLayout.SelectedIndex >= 0) ControllerTypeCombo.SelectedIndex = _hardwareLayout.SelectedIndex; };
        HardwareDeviceHost.Children.Add(PresentationText("Controller layout", "CaptionFontSize", "MutedBrush"));
        HardwareDeviceHost.Children.Add(_hardwareLayout);
        HardwareDeviceHost.Children.Add(PresentationButton("Reconnect", "ReconnectController", () =>
        {
            _app.Service?.RequestReconnect(); _deviceSignature = "";
            _reconnectAt = DateTime.UtcNow; _reconnectPending = true;
            if (_hardwareNotice != null) _hardwareNotice.Text = "Looking for connected controllers…";
        }));
        HardwareDeviceHost.Children.Add(PresentationButton("Identify an input", "IdentifyInput", () => OpenIdentifyInput(this, new RoutedEventArgs())));
        HardwareDeviceHost.Children.Add(PresentationButton("My saved accessories", "IdentifyAccessories", () => OpenAccessories(this, new RoutedEventArgs())));
        _hardwareInfo = PresentationText("Connect a controller to begin.", "CaptionFontSize", "MutedBrush");
        HardwareDeviceHost.Children.Add(_hardwareInfo);
        _hardwareNotice = PresentationText("", "CaptionFontSize", "MutedBrush");
        HardwareDeviceHost.Children.Add(_hardwareNotice); AccessibilitySupport.AnnounceChanges(_hardwareNotice);
        _inputAnnouncement = PresentationText("", "CaptionFontSize", "MutedBrush");
        HardwareDeviceHost.Children.Add(_inputAnnouncement); AccessibilitySupport.AnnounceChanges(_inputAnnouncement);
        foreach (string category in new[] { "Pointer & mouse", "Keyboard", "Windows", "Accessibility", "Nothing" })
        {
            var button = PresentationButton(category, "ActionCategory_" + category.Replace(" ", ""), () =>
            {
                _actionCategory = category; BuildActionChoices();
            });
            var content = new Grid();
            content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new());
            var marker = ButtonText("✓"); marker.Margin = new Thickness(0, 0, 6, 0);
            marker.VerticalAlignment = VerticalAlignment.Center;
            var label = ButtonText(category); label.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(label, 1); content.Children.Add(marker); content.Children.Add(label);
            button.Content = content;
            ActionCategoriesHost.Children.Add(button);
        }
        _presentationReady = true;
        PopulatePresentationOptions(); BuildActionChoices();
        RefreshPresentation(_app.Service?.Status ?? new(false, true, false, -1, "Unavailable", default, null, false));
    }
    private void BuildInspector(StackPanel host, string prefix)
    {
        var name = PresentationText("A button"); name.FontWeight = FontWeights.SemiBold;
        var action = PresentationText("Left click", "SectionFontSize"); action.FontWeight = FontWeights.SemiBold; action.Margin = new Thickness(0);
        var category = PresentationText("Mouse & pointer", "CaptionFontSize", "MutedBrush"); category.Margin = new Thickness(0, 4, 0, 0);
        var icon = new ControllerInputIcon { Width = 60, Height = 60, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top };
        var heading = new Grid(); heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); heading.ColumnDefinitions.Add(new());
        var titles = new StackPanel(); titles.Children.Add(name); titles.Children.Add(action); titles.Children.Add(category); Grid.SetColumn(titles, 1);
        heading.Children.Add(icon); heading.Children.Add(titles);
        var state = PresentationText("No controller", "CaptionFontSize", "MutedBrush");
        state.Margin = new Thickness(0);
        var badge = new Border { Child = state, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 14, 0, 14), HorizontalAlignment = HorizontalAlignment.Left };
        badge.SetResourceReference(Border.BackgroundProperty, "StatusSurfaceBrush");
        badge.SetResourceReference(Border.BorderBrushProperty, "BorderStrongBrush");
        var details = PresentationText("", "CaptionFontSize", "MutedBrush");
        var buttons = new WrapPanel();
        var change = PresentationButton("Change assignment", prefix + "ChangeAction", () =>
        {
            if (_selectedInput == "LeftAxis") SelectPage(1);
            else if (_selectedInput == "RightAxis") SelectPage(5);
            else if (SelectedMappedControl() is Control control)
            {
                if (_portsView) { SetPortsView(false); SelectPresentedInput(control.ToString()); }
                MappingControl.SelectedItem = ControlMapping.Controls.First(option => option.Control == control);
                SelectPage(6); MappingChoicesHost.BringIntoView();
                MappingChoicesHost.Children.OfType<Button>().FirstOrDefault()?.Focus();
            }
            else OpenAccessories(this, new RoutedEventArgs());
        });
        var clear = PresentationButton("Clear mapping", prefix + "ClearMapping", () => SetPresentedBinding(new()));
        change.Style = (Style)FindResource("Primary");
        foreach (var button in new[] { change, clear })
        {
            button.Padding = new Thickness(12, 8, 12, 8); button.SetResourceReference(Button.FontSizeProperty, "MainFontSize");
            if (button.Content is TextBlock label) { label.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize"); label.TextWrapping = TextWrapping.Wrap; }
        }
        buttons.Children.Add(change);
        buttons.Children.Add(clear);
        var rows = new Grid { Margin = new Thickness(0, 16, 0, 8) };
        rows.ColumnDefinitions.Add(new()); rows.ColumnDefinitions.Add(new());
        rows.RowDefinitions.Add(new() { Height = GridLength.Auto }); rows.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var typeLabel = PresentationText("Input state", "CaptionFontSize", "MutedBrush");
        var repeatLabel = PresentationText("Repeat", "CaptionFontSize", "MutedBrush"); Grid.SetRow(repeatLabel, 1);
        var type = PresentationText("Mouse", "CaptionFontSize"); type.FontWeight = FontWeights.SemiBold; Grid.SetColumn(type, 1);
        var repeat = PresentationText("Off", "CaptionFontSize"); repeat.FontWeight = FontWeights.SemiBold; Grid.SetColumn(repeat, 1); Grid.SetRow(repeat, 1);
        foreach (var label in new[] { typeLabel, repeatLabel, type, repeat }) rows.Children.Add(label);
        var divider = new Border { Height = 1, Margin = new Thickness(0, 12, 0, 0) }; divider.SetResourceReference(Border.BackgroundProperty, "BorderDefaultBrush");
        host.Children.Add(heading); host.Children.Add(badge); host.Children.Add(buttons);
        if (prefix == "Control")
        {
            var options = new StackPanel(); options.Children.Add(divider); options.Children.Add(rows);
            var extra = new Expander { Header = "More input options", Content = options, Margin = new Thickness(0, 12, 0, 0) };
            AutomationProperties.SetName(extra, "More input options"); AutomationProperties.SetAutomationId(extra, "ControlInputOptions");
            host.Children.Add(extra);
        }
        else { host.Children.Add(divider); host.Children.Add(rows); }
        host.Children.Add(details);
        _inspectors.Add(new(name, action, category, state, badge, details, change, clear, icon, type, repeat));
    }
    private void SelectPresentedInput(string id)
    {
        if (!_presentedInputs.ContainsKey(id)) return;
        _selectedInput = id;
        if (Pages.SelectedIndex == 9) HardwareInputDetails.IsExpanded = true;
        foreach (var surface in new[] { LiveSurface, MappingSurface, HardwareSurface }) surface.SelectedInput = id;
        if (Enum.TryParse<Control>(id, out var control))
            MappingControl.SelectedItem = ControlMapping.Controls.First(option => option.Control == control);
        BuildActionChoices(); UpdateInspector(); UpdateTiles(); UpdateSimpleControlViews();
    }
    private void SetPortsView(bool ports)
    {
        bool changed = _portsView != ports;
        _portsView = ports;
        LiveSurface.PortsView = HardwareSurface.PortsView = ports;
        foreach (var button in _frontButtons) UpdateViewButton(button, !ports, "Front view");
        foreach (var button in _portButtons) UpdateViewButton(button, ports, "Ports view");
        if (changed) UpdateAccessibleViewport();
        UpdateInspector(); UpdateSimpleControlViews();
    }
    private void UpdateViewButton(Button button, bool selected, string label)
    {
        string caption = label;
        if (button.Content is not TextBlock text || text.Text != caption) button.Content = ButtonText(caption);
        button.SetResourceReference(Button.BackgroundProperty, selected ? "SelectionSurfaceBrush" : "PanelBrush");
        button.SetResourceReference(Button.ForegroundProperty, selected ? "SelectionTextBrush" : "TextBrush");
        button.SetResourceReference(Button.BorderBrushProperty, selected ? "TextPrimaryBrush" : "BorderDefaultBrush");
        button.BorderThickness = selected ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 0, 1);
        AutomationProperties.SetItemStatus(button, selected ? "Selected view" : "");
    }
    private void PopulatePresentationOptions()
    {
        if (ShowLabelsCheck == null) return;
        bool loading = _loading; _loading = true;
        ShowLabelsCheck.IsChecked = _settings.ShowControllerLabels;
        ReduceMotionCheck.IsChecked = _settings.ReduceInterfaceMotion;
        InputAnnouncementsCheck.IsChecked = _settings.InputAnnouncements;
        ReadHoveredTextCheck.IsChecked = _settings.ReadHoveredText;
        _loading = loading;
    }
    private void PresentationOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !_presentationReady) return;
        bool wasReadingHoveredText = _settings.ReadHoveredText;
        _settings = _settings with
        {
            ShowControllerLabels = ShowLabelsCheck.IsChecked == true,
            ReduceInterfaceMotion = ReduceMotionCheck.IsChecked == true,
            InputAnnouncements = InputAnnouncementsCheck.IsChecked == true,
            ReadHoveredText = ReadHoveredTextCheck.IsChecked == true,
            VisualAccess = _settings.EffectiveVisualAccess with
            {
                Motion = ReduceMotionCheck.IsChecked == true
                ? _settings.EffectiveVisualAccess.Motion == VisualMotion.Off ? VisualMotion.Off : VisualMotion.Reduced : VisualMotion.Standard
            }
        };
        if (_settings.ReadHoveredText != wasReadingHoveredText)
            foreach (Window window in Application.Current.Windows) RelayHoverReader.ForWindow(window)?.Cancel();
        ReadSettings(updateInput: false); SaveSettings(); RefreshStatus();
    }
    private string InputName(string id, bool adaptive) => id switch
    {
        "A" when adaptive => "Left large button A",
        "B" when adaptive => "Right large button B",
        "A" or "B" or "X" or "Y" => id + " button",
        "LeftAxis" => "Left stick movement",
        "RightAxis" => "Right stick movement",
        "Xbox" => "Xbox button",
        "Profile" => "Profile button",
        "X1" => "X1 joystick socket",
        "X2" => "X2 joystick socket",
        "USBLeft" => "Left USB port",
        "USBRight" => "Right USB port",
        _ => ControlMapping.Controls.FirstOrDefault(option => option.Control.ToString() == id)?.Name ?? id
    };
    private void RefreshPresentation(ControllerStatus status)
    {
        if (!_presentationReady) return;
        bool showingController = ControlWorkspace.Visibility == Visibility.Visible;
        ControlDisconnectedPane.Visibility = status.Connected ? Visibility.Collapsed : Visibility.Visible;
        ControlWorkspace.Visibility = StatusInstructionLine.Visibility = status.Connected ? Visibility.Visible : Visibility.Collapsed;
        // Polling may return early when the controller state has not changed.
        // Apply the input-list visibility before that fast path without exposing
        // the full row alongside the user's deliberately simplified single card.
        ControlTilesHost.Visibility = status.Connected && !SimpleControlsEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (showingController != status.Connected) AccessibilitySupport.AnnounceAgain(status.Connected ? StatusDetail : DisconnectedInstructionText);
        bool adaptive = _settings.ControllerType == ControllerPreference.Adaptive || _app.Service?.ControllerKind == ControllerKind.Adaptive;
        bool hardwareLayoutChanged = adaptive != _lastPresentationContext.Adaptive;
        var context = (adaptive, Pages.SelectedIndex, _app.Service?.KeyboardNavigationActive == true, _app.Service?.KeyboardSelectionActive == true);
        if (status == _lastPresentationStatus && ReferenceEquals(_settings, _lastPresentationSettings) && context == _lastPresentationContext)
        { if (DateTime.UtcNow >= _nextDeviceRefresh) { UpdateDevices(status); _nextDeviceRefresh = DateTime.UtcNow.AddSeconds(1); } return; }
        _lastPresentationStatus = status; _lastPresentationSettings = _settings; _lastPresentationContext = context;
        var pad = status.State;
        foreach (var option in ControlMapping.Controls)
        {
            string id = option.Control.ToString();
            var binding = Pages.SelectedIndex == 0 ? _app.Service?.ResolveBinding(option.Control) ?? ControlMapping.Resolve(_settings, option.Control) : ControlMapping.Resolve(_settings, option.Control);
            bool down = status.Connected && ControlMapping.Down(pad, option.Control);
            double value = option.Control == Control.LeftTrigger ? pad.LeftTrigger / 255d : option.Control == Control.RightTrigger ? pad.RightTrigger / 255d : down ? 1 : 0;
            var accessory = (_settings.AdaptiveAccessories ?? []).FirstOrDefault(a => a.Signals.Contains((AccessorySignal)option.Control));
            _presentedInputs[id] = new(id, InputName(id, adaptive), BindingName(binding), !status.Connected ? "No controller connected" : down ? "Pressed" : "Released",
                binding.Kind.ToString(), down, status.Connected, true, binding.Repeat, null, accessory?.Name, Value: value);
        }
        foreach (string id in new[] { "LeftAxis", "RightAxis" })
        {
            bool left = id == "LeftAxis";
            double x = (left ? pad.LeftX : pad.RightX) / 32768d, y = (left ? pad.LeftY : pad.RightY) / 32768d;
            bool moving = status.Connected && InputProcessor.Shape(left ? pad.LeftX : pad.RightX, left ? pad.LeftY : pad.RightY, left ? _settings.DeadZone : _settings.ScrollDeadZone, 1) != (0d, 0d);
            var accessory = (_settings.AdaptiveAccessories ?? []).FirstOrDefault(a => a.Signals.Contains(left ? AccessorySignal.LeftAxis : AccessorySignal.RightAxis));
            _presentedInputs[id] = new(id, InputName(id, adaptive), left ? "Move pointer" : "Scroll", !status.Connected ? "No controller connected" : moving ? "Moving" : "Centred",
                left ? "Pointer movement" : "Scrolling", moving, status.Connected, true, false, AccessoryName: accessory?.Name, X: x, Y: y);
        }
        foreach (string id in new[] { "Xbox", "Profile" })
            _presentedInputs[id] = new(id, InputName(id, adaptive), id == "Xbox" ? "Reserved by Windows" : "Managed by the controller",
                "Input is not reported to Relay", "Hardware", false, status.Connected, false, false);
        foreach (var port in AdaptiveAccessory.Ports.Where(port => port.Id is "X1" or "X2" or "USBLeft" or "USBRight"))
        {
            var accessory = (_settings.AdaptiveAccessories ?? []).FirstOrDefault(a => a.Port == port.Id);
            bool active = status.Connected && accessory?.Signals.Any(signal => AdaptiveAccessory.Active(signal, pad)) == true;
            _presentedInputs[port.Id] = new(port.Id, port.Name, accessory == null ? "No saved accessory" : string.Join(", ", accessory.Signals.Select(signal => _presentedInputs.GetValueOrDefault(signal.ToString())?.Action ?? AdaptiveAccessory.SignalName(signal))),
                active ? "Matching input active · connection not reported" : "Connection not reported", "Accessory port", active, status.Connected, false, false, port.Id, accessory?.Name);
        }
        foreach (var surface in new[] { LiveSurface, MappingSurface, HardwareSurface })
        {
            surface.Connected = status.Connected; surface.Adaptive = adaptive; surface.State = pad;
            surface.MinHeight = adaptive ? 320 : 390;
            surface.ShowLabels = _settings.ShowControllerLabels && _settings.EffectiveVisualAccess.Complexity == VisualComplexity.Standard;
            surface.ReducedMotion = _settings.EffectiveVisualAccess.Motion != VisualMotion.Standard || !SystemParameters.ClientAreaAnimation;
            surface.Accessories = _settings.AdaptiveAccessories ?? [];
            surface.DeadZone = _settings.DeadZone; surface.ScrollDeadZone = _settings.ScrollDeadZone;
            surface.Inputs = new Dictionary<string, InputPresentation>(_presentedInputs);
            surface.SelectedInput = _selectedInput;
        }
        foreach (var button in _frontButtons.Concat(_portButtons)) button.Visibility = adaptive ? Visibility.Visible : Visibility.Collapsed;
        if (adaptive) SetPortsView(_portsView);
        if (!adaptive && _portsView) SetPortsView(false);
        if (hardwareLayoutChanged) NormalizePresentedInputForLayout();
        string tileKind = adaptive ? "Adaptive" : "Standard";
        if (_tileKind != tileKind) { _tileKind = tileKind; BuildInputTiles(adaptive); }
        UpdateTiles(); UpdateInspector(); UpdateDevices(status); UpdateLastInput(status); UpdateSimpleControlViews();
        if (_inputAnnouncement != null)
        {
            string activeInput = string.Join(", ", _presentedInputs.Values.Where(input => input.IsActive && Enum.TryParse<Control>(input.Id, out _)).Select(input => FriendlyInputName(input) + ". " + HumanAction(input.Action)));
            if (_settings.InputAnnouncements && activeInput != _lastAnnouncedInput && activeInput.Length > 0)
            {
                _inputAnnouncement.Text = activeInput + " pressed.";
                AccessibilitySupport.AnnounceAgain(_inputAnnouncement);
            }
            if (!_settings.InputAnnouncements) _inputAnnouncement.Text = "";
            _lastAnnouncedInput = activeInput;
        }
    }
    private void BuildInputTiles(bool adaptive)
    {
        ControlTilesHost.Children.Clear(); HardwareInputsHost.Children.Clear(); _controlTiles.Clear(); _hardwareTiles.Clear();
        foreach (string id in adaptive ? new[] { "A", "B", "Up", "Back", "Start", "X1", "X2" } : new[] { "A", "B", "X", "Y", "LeftAxis", "RightAxis", "Up", "Back", "Start" })
            AddInputTile(ControlTilesHost, _controlTiles, id, "ControlInput");
        foreach (string id in adaptive ? AdaptiveAccessory.Ports.Select(port => port.Id).Concat(new[] { "LeftAxis", "RightAxis", "Profile" }) : _presentedInputs.Keys.Where(id => id is not ("Profile" or "X1" or "X2" or "USBLeft" or "USBRight")))
            AddInputTile(HardwareInputsHost, _hardwareTiles, id, "HardwareInput");
        RefreshNavigationGroups();
    }
    private void AddInputTile(WrapPanel host, Dictionary<string, InputTile> tiles, string id, string prefix)
    {
        var panel = new StackPanel();
        var icon = new ControllerInputIcon { Input = id, Adaptive = _tileKind == "Adaptive", Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3) };
        var name = PresentationText("", "CaptionFontSize"); name.TextAlignment = TextAlignment.Center; name.FontWeight = FontWeights.SemiBold; name.Margin = new Thickness(0, 0, 0, 1);
        var action = PresentationText("", "MainFontSize", "MutedBrush"); action.TextAlignment = TextAlignment.Center; action.FontWeight = FontWeights.Normal; action.Margin = new Thickness(0);
        var state = PresentationText("", "CaptionFontSize"); state.TextAlignment = TextAlignment.Center; state.Margin = new Thickness(0);
        var badge = new Border { Child = state, CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(icon); panel.Children.Add(name); panel.Children.Add(action); panel.Children.Add(badge);
        var button = new Button { Content = panel, Width = 112, MinHeight = 110, Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        button.Style = (Style)FindResource("InputTile");
        AutomationProperties.SetAutomationId(button, prefix + "_" + id);
        AutomationProperties.SetHelpText(button, "Choose to inspect this input. Choosing it does not send its action to Windows.");
        button.Click += (_, _) => SelectPresentedInput(id);
        host.Children.Add(button); tiles[id] = new(button, name, action, state, icon, badge);
        UpdateAccessibleViewport();
    }
    private void UpdateTiles()
    {
        foreach (var entry in _controlTiles.Concat(_hardwareTiles))
        {
            if (!_presentedInputs.TryGetValue(entry.Key, out var input)) continue;
            bool selected = _selectedInput == input.Id;
            entry.Value.Name.Text = input.AccessoryName ?? TileName(input.Id, _tileKind == "Adaptive");
            entry.Value.Action.Text = TileAction(input);
            entry.Value.State.Text = input.IsActive ? selected ? "✓ Pressed" : "● Pressed" : selected ? "✓ Selected" : "Ready";
            // Keep the single-line state space as focus, selection and input change.
            // Otherwise selecting a two-line action can push the entire row below the viewport.
            entry.Value.Badge.Visibility = input.IsActive || selected ? Visibility.Visible : Visibility.Hidden;
            entry.Value.Badge.SetResourceReference(Border.BackgroundProperty, selected ? "SelectionSurfaceBrush" : "SuccessSurfaceBrush");
            entry.Value.State.SetResourceReference(TextBlock.ForegroundProperty, input.IsActive && !selected ? "SuccessBrush" : "TextBrush");
            entry.Value.Button.SetResourceReference(Button.BackgroundProperty, selected ? "SelectionSurfaceBrush" : input.IsActive ? "SuccessSurfaceBrush" : "PanelBrush");
            entry.Value.Button.SetResourceReference(Button.BorderBrushProperty, "BorderDefaultBrush");
            InputTileState.SetIsSelected(entry.Value.Button, selected); InputTileState.SetIsActive(entry.Value.Button, input.IsActive);
            AutomationProperties.SetName(entry.Value.Button, FriendlyInputName(input) + ". " + HumanAction(input.Action) + (input.AccessoryName == null ? "" : ". " + input.Name));
            AutomationProperties.SetItemStatus(entry.Value.Button, (selected ? "Selected. " : "") + input.State);
        }
    }
    private static string TileName(string id, bool adaptive) => id switch
    {
        "A" or "B" when adaptive => "Large pad " + id,
        "A" or "B" or "X" or "Y" => id,
        "LeftAxis" => "Left stick",
        "RightAxis" => "Right stick",
        "LeftStick" => "Left stick press",
        "RightStick" => "Right stick press",
        "Up" => "D-pad up",
        "Down" => "D-pad down",
        "Left" => "D-pad left",
        "Right" => "D-pad right",
        "Back" => "View",
        "Start" => "Menu",
        "LeftShoulder" => "LB",
        "RightShoulder" => "RB",
        "LeftTrigger" => "LT",
        "RightTrigger" => "RT",
        "USBLeft" => "Left USB",
        "USBRight" => "Right USB",
        "Xbox" => "Xbox",
        "Profile" => "Profile",
        _ => id
    };
    private static string TileAction(InputPresentation input)
    {
        if (input.Id == "LeftAxis") return "Pointer";
        if (input.Id == "RightAxis") return "Scroll";
        if (input.Id is "Xbox" or "Profile") return "Reserved";
        string action = HumanAction(input.Action).Replace(" / drag", "", StringComparison.Ordinal);
        if (action.Contains("accessibility settings", StringComparison.OrdinalIgnoreCase)) return "Accessibility";
        if (action.Contains("Windows Start", StringComparison.OrdinalIgnoreCase)) return "Start menu";
        if (action.Contains("voice typing", StringComparison.OrdinalIgnoreCase)) return "Voice typing";
        if (input.Type == "Pointer") return "Pointer " + input.Id.ToLowerInvariant();
        if (action.Length <= 20) return action;
        // Compact visual labels retain the complete action in the accessible name and inspector.
        return input.Type switch { "Keyboard" => "Keyboard shortcut", "Shortcut" => "Windows shortcut", "Accessory port" => "Saved setup", _ => action };
    }
    private void UpdateInspector()
    {
        if (!_presentedInputs.TryGetValue(_selectedInput, out var input)) return;
        input = PresentInputForView(input, _portsView && Pages.SelectedIndex != 6);
        foreach (var inspector in _inspectors)
        {
            inspector.Name.Text = FriendlyInputName(input);
            inspector.Icon.Input = input.Id; inspector.Icon.Adaptive = _tileKind == "Adaptive";
            inspector.Action.Text = HumanAction(input.Action);
            inspector.Category.Text = input.Type switch
            {
                "Mouse" or "Pointer" or "Pointer movement" => "Mouse & pointer",
                "Precision" or "Boost" => "Pointer speed",
                "Shortcut" => "Windows shortcut",
                "Saved accessory setup" or "Accessory port" => "Adaptive accessory",
                _ => input.Type
            };
            inspector.State.Text = input.IsActive ? "● " + input.State : input.State;
            inspector.State.SetResourceReference(TextBlock.ForegroundProperty, input.IsActive ? "SuccessBrush" : "MutedBrush");
            inspector.Badge.SetResourceReference(Border.BackgroundProperty, input.IsActive ? "SuccessSurfaceBrush" : "StatusSurfaceBrush");
            inspector.StateValue.Text = input.State; inspector.Repeat.Text = input.Repeat ? "On" : "Off";
            inspector.Details.Text = (input.AccessoryName == null ? "" : "Input: " + input.Name) +
                (input.Port == null ? "" : "\nPort: " + input.Port + "\nPhysical accessory connection is not reported by Windows.") +
                (input.Id == "LeftAxis" ? $"\nSpeed: {_settings.PointerSpeed:0} pixels per second\nDead zone: {_settings.DeadZone:P0}" :
                 input.Id == "RightAxis" ? $"\nScroll speed: {_settings.ScrollSpeed:0}\nDead zone: {_settings.ScrollDeadZone:P0}" : "");
            if (input.Id is "LeftAxis" or "RightAxis") inspector.Details.Text += $"\nStick position: horizontal {input.X * 100:0}%, vertical {input.Y * 100:0}%";
            if (input.Id is "LeftTrigger" or "RightTrigger") inspector.Details.Text += $"\nTrigger travel: {input.Value * 100:0}%";
            if (_settings.EffectiveVisualAccess.Complexity != VisualComplexity.Standard)
                inspector.Details.Text = input.Port == null ? "" : "Physical connection is not reported.";
            inspector.Change.IsEnabled = input.CanMap || input.Port != null;
            bool unassigned = HumanAction(input.Action) == "Unassigned";
            string caption = input.Id is "LeftAxis" or "RightAxis" ? "Adjust movement" : input.CanMap ? unassigned ? "Assign action" : "Change assignment" : "Name accessory";
            if (inspector.Change.Content is not TextBlock old || old.Text != caption)
            {
                var label = ButtonText(caption); label.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize"); label.TextWrapping = TextWrapping.Wrap; inspector.Change.Content = label;
            }
            AutomationProperties.SetName(inspector.Change, (input.Id is "LeftAxis" or "RightAxis" ? "Adjust movement for " : input.CanMap ? unassigned ? "Assign action for " : "Change assignment for " : "Name accessory for ") + FriendlyInputName(input));
            inspector.Clear.IsEnabled = input.CanMap && SelectedMappedControl().HasValue;
        }
    }
    private InputPresentation PresentInputForView(InputPresentation input, bool ports)
    {
        var port = ports ? AdaptiveAccessory.Ports.FirstOrDefault(port => port.Id == input.Id) : null;
        if (port != null)
        {
            var accessory = (_settings.AdaptiveAccessories ?? []).FirstOrDefault(accessory => accessory.Port == port.Id);
            var signals = accessory?.Signals ?? [port.DefaultSignal];
            string action = string.Join(", ", signals.Select(signal => _presentedInputs.GetValueOrDefault(signal.ToString())?.Action ?? AdaptiveAccessory.SignalName(signal)));
            bool matching = input.Connected && signals.Any(signal => AdaptiveAccessory.Active(signal, _lastPresentationStatus?.State ?? default));
            input = input with
            {
                Name = port.Name,
                Action = action.Length == 0 ? "Input not recorded" : action,
                CanMap = accessory?.Signals.Length == 1 && (int)accessory.Signals[0] < 16,
                AccessoryName = accessory?.Name,
                Port = port.Id,
                IsActive = matching,
                State = (matching ? "Matching logical input active" : "Logical input inactive") + " · connection not reported",
                Type = "Saved accessory setup"
            };
        }
        return input;
    }
    private void UpdateDevices(ControllerStatus status)
    {
        if (_hardwareDevices == null) return;
        _devicesLoading = true; if (_hardwareLayout != null) _hardwareLayout.SelectedIndex = (int)_settings.ControllerType; _devicesLoading = false;
        var devices = _app.Service?.Devices ?? Array.Empty<ControllerDevice>();
        if (_reconnectPending && (DateTime.UtcNow - _reconnectAt).TotalSeconds >= 1)
        {
            _reconnectPending = false;
            if (_hardwareNotice != null) _hardwareNotice.Text = devices.Count == 0 ? "No controller detected. Check the connection, then try Reconnect." : $"Found {devices.Count} controller{(devices.Count == 1 ? "" : "s")}. Release the controls to continue.";
        }
        string signature = string.Join("|", devices.Select(device => $"{device.Slot}:{device.Name}:{device.Selected}"));
        if (signature != _deviceSignature)
        {
            _devicesLoading = true; _hardwareDevices.ItemsSource = devices; _hardwareDevices.SelectedValue = devices.FirstOrDefault(device => device.Selected)?.Slot;
            _hardwareDevices.IsEnabled = devices.Count > 0; _devicesLoading = false; _deviceSignature = signature;
        }
        if (_hardwareInfo != null) _hardwareInfo.Text = !status.Connected ? "No controller detected. Connect your controller, then choose Reconnect." :
            (_app.Service?.ControllerModel ?? "Controller preview") + $"\nController {status.Slot + 1}\nBattery: {status.Battery}\nUSB or wireless connection type is not reported.\nAccessory names describe your saved setup.";
    }
    private IEnumerable<BindingOption> PresentationActions()
    {
        if (_actionCategory == "Nothing") { yield return new("No action", new()); yield break; }
        if (_actionCategory == "Pointer & mouse")
        {
            foreach (var mouse in Enum.GetValues<ControllerMouse.Core.MouseButton>()) yield return new(mouse + " click", new(BindingKind.Mouse, mouse));
            yield return new("Hold for precision", new(BindingKind.Precision)); yield return new("Hold for speed boost", new(BindingKind.Boost));
            foreach (var (name, key) in new[] { ("Left", 37), ("Up", 38), ("Right", 39), ("Down", 40) }) yield return new("Move pointer " + name.ToLowerInvariant(), new(BindingKind.Pointer, Key: key));
        }
        else if (_actionCategory == "Keyboard")
        {
            foreach (var (name, key, modifiers) in new[] { ("Enter", 13, 0), ("Escape", 27, 0), ("Next control (Tab)", 9, 0), ("Previous control (Shift+Tab)", 9, 4), ("Space", 32, 0), ("Backspace", 8, 0), ("Arrow left", 37, 0), ("Arrow up", 38, 0), ("Arrow right", 39, 0), ("Arrow down", 40, 0) })
                yield return new(name, new(BindingKind.Keyboard, Key: key, Modifiers: modifiers) { Repeat = key is >= 37 and <= 40 });
        }
        else
            foreach (var action in AccessibilityCatalog.Actions.Where(action => action.Action != Shortcut.None &&
                (action.Action != Shortcut.LiveCaptions || Environment.OSVersion.Version.Build >= 22621) &&
                (_actionCategory == "Windows" ? action.Action is Shortcut.StartMenu or Shortcut.ScreenSnipping || (int)action.Action >= 100 : action.Action != Shortcut.StartMenu && action.Action != Shortcut.ScreenSnipping && (int)action.Action < 100)))
                yield return new(action.Name, new(BindingKind.Shortcut, Shortcut: action.Action));
    }
    private void BuildActionChoices()
    {
        if (!_presentationReady) return;
        foreach (var button in ActionCategoriesHost.Children.OfType<Button>())
        {
            string category = AutomationProperties.GetName(button);
            bool selected = category == _actionCategory;
            // Reserve the selection marker so changing category never moves
            // another target under the pointer or wraps a row unexpectedly.
            if (button.Content is Grid content && content.Children[0] is TextBlock marker)
                marker.Visibility = selected ? Visibility.Visible : Visibility.Hidden;
            button.SetResourceReference(Button.BackgroundProperty, selected ? "SelectionSurfaceBrush" : "PanelBrush");
            button.SetResourceReference(Button.ForegroundProperty, selected ? "SelectionTextBrush" : "TextBrush");
            button.SetResourceReference(Button.BorderBrushProperty, selected ? "TextPrimaryBrush" : "BorderStrongBrush");
            button.BorderThickness = new Thickness(1);
            AutomationProperties.SetItemStatus(button, selected ? "Selected category" : "");
        }
        MappingChoicesHost.Children.Clear();
        bool mapsButton = Enum.TryParse<Control>(_selectedInput, out var control);
        if (!mapsButton)
        {
            MappingChoicesHost.Children.Add(PresentationText("Choose a controller button to change its action. Choose Adjust movement for a stick, or Hardware to name an accessory.")); return;
        }
        foreach (var option in PresentationActions().Where(option => control is not (Control.Back or Control.Start) || option.Binding.Kind is not (BindingKind.Precision or BindingKind.Boost or BindingKind.Pointer) && !option.Binding.Repeat))
        {
            var binding = option.Binding;
            var button = PresentationButton(option.Name, "ChooseAction_" + option.Name.Replace(" ", ""), () => SetPresentedBinding(binding));
            button.Width = 220;
            AutomationProperties.SetHelpText(button, "Assign this action to " + InputName(_selectedInput, _settings.ControllerType == ControllerPreference.Adaptive) + ". Release controller buttons before testing it on the Control page.");
            MappingChoicesHost.Children.Add(button);
        }
        if (_actionCategory == "Keyboard")
        {
            var recorder = PresentationButton("Record another shortcut", "RecordPresentationShortcut", () => { }); recorder.Tag = "Presentation";
            recorder.Click += (_, _) => CaptureShortcut(recorder, new RoutedEventArgs()); MappingChoicesHost.Children.Add(recorder);
        }
    }
    private void SetPresentedBinding(ControlBinding binding)
    {
        if (SelectedMappedControl() is not Control control) return;
        var mappings = _settings.ButtonMappings == null ? new Dictionary<Control, ControlBinding>() : new(_settings.ButtonMappings);
        mappings[control] = binding;
        _settings = (_settings with { ButtonMappings = mappings }).Validate();
        _syncingPresentationEditor = true;
        try { MappingControl.SelectedItem = ControlMapping.Controls.First(option => option.Control == control); LoadBinding(); }
        finally { _syncingPresentationEditor = false; }
        ReadSettings(); SaveSettings(); BuildGuide(); RefreshStatus();
        MappingNotice.Text = InputName(_selectedInput, _settings.ControllerType == ControllerPreference.Adaptive) + ": " + BindingName(binding) +
            (_saveError == null ? ". Saved. " : ". Applied for this session. Settings could not be saved. ") + "Choose Control, release the controls, then try it.";
    }
}

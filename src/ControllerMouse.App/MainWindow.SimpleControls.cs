using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private sealed record SimpleInputView(StackPanel Host, ControllerSurface Surface, SettingChoiceButton Picker, Button Inspect,
        Button Previous, Button Next, ControllerInputIcon Icon, TextBlock Name, TextBlock Position, TextBlock Action, TextBlock State, TextBlock Announcement, string Prefix);
    private readonly List<SimpleInputView> _simpleInputViews = [];
    private bool _updatingSimpleInputs;
    private string? _lastInputId;
    private readonly HashSet<string> _previousActiveInputs = new(StringComparer.Ordinal);
    private bool SimpleControlsEnabled => _settings.EffectiveVisualAccess.OneControlAtATime ||
        _settings.EffectiveVisualAccess.Complexity == VisualComplexity.Minimal;

    private void InitializeSimpleControls()
    {
        foreach (var (host, surface, prefix) in new[] { (ControlSimpleHost, LiveSurface, "Control"), (MappingSimpleHost, MappingSurface, "Mapping"), (HardwareSimpleHost, HardwareSurface, "Hardware") })
        {
            var picker = VisualChoiceField(host, "Choose an input", prefix + "SimpleInputChoice", Array.Empty<VisualChoice<string>>());
            var name = PresentationText("A button", "SectionFontSize"); name.FontWeight = FontWeights.SemiBold; name.TextAlignment = TextAlignment.Center;
            var position = PresentationText("", "CaptionFontSize", "MutedBrush"); position.TextAlignment = TextAlignment.Center;
            var icon = new ControllerInputIcon { Input = "A", Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 12) };
            var action = PresentationText("Left click", "SectionFontSize"); action.TextAlignment = TextAlignment.Center;
            var state = PresentationText("Released", "MainFontSize"); state.TextAlignment = TextAlignment.Center;
            var contents = new StackPanel(); contents.Children.Add(name); contents.Children.Add(position); contents.Children.Add(icon); contents.Children.Add(action); contents.Children.Add(state);
            var inspect = new Button { Content = contents, Padding = new Thickness(24), MinHeight = 220, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 16), Style = (Style)FindResource("InputTile") };
            AutomationProperties.SetAutomationId(inspect, prefix + "SimpleInput");
            AutomationProperties.SetHelpText(inspect, "This is the selected input. Its assignment is shown beside or below it. Choosing it does not send the action to Windows.");
            inspect.Click += (_, _) => SelectPresentedInput(_selectedInput);
            var previous = PresentationButton("Previous input", prefix + "SimplePrevious", () => MoveSimpleInput(surface, -1));
            var next = PresentationButton("Next input", prefix + "SimpleNext", () => MoveSimpleInput(surface, 1));
            var movement = new WrapPanel(); movement.Children.Add(previous); movement.Children.Add(next);
            var announcement = PresentationText("", "CaptionFontSize", "MutedBrush");
            AutomationProperties.SetAutomationId(announcement, prefix + "SimpleSelectionSummary");
            AccessibilitySupport.AnnounceChanges(announcement);
            host.Children.Add(movement); host.Children.Add(inspect); host.Children.Add(announcement);
            picker.SelectionChanged += (_, _) => { if (!_updatingSimpleInputs && picker.SelectedValue is string id) { SelectPresentedInput(id); AnnounceSimpleSelection(surface); } };
            _simpleInputViews.Add(new(host, surface, picker, inspect, previous, next, icon, name, position, action, state, announcement, prefix));
        }
        UpdateSimpleControlViews();
    }
    private string[] SimpleInputOrder(ControllerSurface surface) => surface.Parts.Keys.Concat(_presentedInputs.Keys)
        .Where(id => _presentedInputs.ContainsKey(id) && (surface.Adaptive || id is not ("Profile" or "X1" or "X2" or "USBLeft" or "USBRight"))).Distinct().ToArray();
    private void NormalizePresentedInputForLayout()
    {
        var surface = Pages.SelectedIndex == 6 ? MappingSurface : Pages.SelectedIndex == 9 ? HardwareSurface : LiveSurface;
        var order = SimpleInputOrder(surface);
        if (order.Length == 0 || order.Contains(_selectedInput)) return;
        // An explicit controller/layout change can remove an Adaptive-only
        // input. Keep valid selections, and move an unavailable selection once
        // to the new layout's first input. Live presses never select a card.
        _selectedInput = order[0];
        foreach (var view in new[] { LiveSurface, MappingSurface, HardwareSurface }) view.SelectedInput = _selectedInput;
        if (Enum.TryParse<ControllerMouse.Core.Control>(_selectedInput, out var control))
        {
            bool syncing = _syncingPresentationEditor; _syncingPresentationEditor = true;
            try { MappingControl.SelectedItem = ControlMapping.Controls.First(option => option.Control == control); }
            finally { _syncingPresentationEditor = syncing; }
        }
        BuildActionChoices();
    }
    private void MoveSimpleInput(ControllerSurface surface, int direction)
    {
        var order = SimpleInputOrder(surface); if (order.Length == 0) return;
        int current = Array.IndexOf(order, _selectedInput);
        int next = Math.Clamp((current < 0 ? 0 : current) + direction, 0, order.Length - 1);
        SelectPresentedInput(order[next]);
        AnnounceSimpleSelection(surface);
    }
    private void AnnounceSimpleSelection(ControllerSurface surface)
    {
        var view = _simpleInputViews.First(view => view.Surface == surface);
        var input = PresentInputForView(_presentedInputs[_selectedInput], SimplePortsView(surface));
        view.Announcement.Text = "Selected. " + AccessibleInputName(input) + ". " + view.Position.Text + ". " + view.Action.Text + ". " + view.State.Text.TrimStart('●', ' ');
        AccessibilitySupport.AnnounceAgain(view.Announcement);
    }
    private void UpdateSimpleControlViews()
    {
        if (_simpleInputViews.Count == 0) return;
        var visual = _settings.EffectiveVisualAccess;
        bool simple = SimpleControlsEnabled;
        _updatingSimpleInputs = true;
        try
        {
            foreach (var view in _simpleInputViews)
            {
                view.Host.Visibility = simple ? Visibility.Visible : Visibility.Collapsed;
                view.Surface.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
                if (!simple) continue;
                var order = SimpleInputOrder(view.Surface);
                var choices = order.Select(id => new VisualChoice<string>(AccessibleInputName(PresentInputForView(_presentedInputs[id], SimplePortsView(view.Surface))), id)).ToArray();
                if (view.Picker.ItemsSource is not VisualChoice<string>[] old || !old.SequenceEqual(choices)) view.Picker.ItemsSource = choices;
                view.Picker.SelectedValue = _selectedInput;
                if (view.Picker.Parent is UIElement pickerField) pickerField.Visibility = visual.OneControlAtATime ? Visibility.Collapsed : Visibility.Visible;
                if (!_presentedInputs.TryGetValue(_selectedInput, out var input)) input = _presentedInputs.Values.FirstOrDefault();
                if (input == null) continue;
                input = PresentInputForView(input, SimplePortsView(view.Surface));
                view.Name.Text = FriendlyInputName(input);
                view.Action.Text = HumanAction(input.Action);
                view.State.Text = (input.IsActive ? "● " : "") + input.State;
                view.State.SetResourceReference(TextBlock.ForegroundProperty, input.IsActive ? "SuccessBrush" : "TextBrush");
                view.Icon.Input = input.Id; view.Icon.Adaptive = view.Surface.Adaptive;
                view.Icon.Width = view.Icon.Height = 100 * Math.Min(InterfaceTextScale, 1.5);
                view.Inspect.SetResourceReference(Button.BackgroundProperty, "SelectionSurfaceBrush");
                InputTileState.SetIsSelected(view.Inspect, true); InputTileState.SetIsActive(view.Inspect, input.IsActive);
                AutomationProperties.SetName(view.Inspect, AccessibleInputName(input) + ". " + HumanAction(input.Action));
                int index = Array.IndexOf(order, _selectedInput);
                view.Position.Text = index >= 0 ? $"Input {index + 1} of {order.Length}" : "Selected input";
                AutomationProperties.SetItemStatus(view.Inspect, "Selected. " + view.Position.Text + ". " + input.State);
                AutomationProperties.SetItemStatus(view.Previous, index <= 0 ? "First input" : "");
                AutomationProperties.SetItemStatus(view.Next, index >= order.Length - 1 ? "Last input" : "");
            }
            bool connected = _lastPresentationStatus?.Connected == true;
            ControlTilesHost.Visibility = connected && !simple ? Visibility.Visible : Visibility.Collapsed;
            HardwareInputsHost.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
            PreviewCaption.Visibility = visual.Complexity == VisualComplexity.Standard && _settings.ShowControllerLabels && !simple ? Visibility.Visible : Visibility.Collapsed;
            LastInputPanel.Visibility = connected && !simple ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _updatingSimpleInputs = false; }
        RefreshNavigationGroups();
    }
    private static string FriendlyInputName(InputPresentation input) => input.AccessoryName ?? input.Name;
    private bool SimplePortsView(ControllerSurface surface) => surface != MappingSurface && surface.Adaptive && _portsView;
    private static string AccessibleInputName(InputPresentation input) => FriendlyInputName(input) + (input.AccessoryName == null ? "" : ", " + input.Name);
    private static string HumanAction(string action) => action is "No assignment" or "No action" or "None" ? "Unassigned" : action;

    private void UpdateLastInput(ControllerStatus status)
    {
        if (!status.Connected)
        {
            _lastInputId = null; _previousActiveInputs.Clear(); LastInputSummary.Text = "No controller connected"; return;
        }
        var active = _presentedInputs.Values.Where(input => input.IsActive && Enum.TryParse<ControllerMouse.Core.Control>(input.Id, out _)).ToArray();
        var newlyPressed = active.LastOrDefault(input => !_previousActiveInputs.Contains(input.Id));
        if (newlyPressed != null) _lastInputId = newlyPressed.Id;
        _previousActiveInputs.Clear(); foreach (var input in active) _previousActiveInputs.Add(input.Id);
        if (_lastInputId != null && _presentedInputs.TryGetValue(_lastInputId, out var last))
        {
            LastInputSummary.Text = FriendlyInputName(last) + (last.AccessoryName == null ? "" : " · " + last.Name) + " · " + HumanAction(last.Action);
            AutomationProperties.SetName(LastInputSummary, "Last input. " + LastInputSummary.Text);
        }
        else LastInputSummary.Text = "No input yet";
    }
}

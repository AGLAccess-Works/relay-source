using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private readonly Dictionary<string, Border> _mappingRows = new();
    private string _lastPhysical = "Nothing yet", _lastRecognised = "No controller", _lastCode = "";
    private DateTime _inputAt = DateTime.MinValue;
    private readonly string[] _pageNames = ["Control", "Pointer", "Assistance", "Accessibility", "Help", "Scrolling", "Mapping", "Settings", "Profiles", "Hardware", "About"];
    private static int NavigationPage(int page) => page is 1 or 2 or 5 ? 6 : page;
    private bool _syncingNavigation;
    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (!_syncingNavigation && Pages != null && sender is FrameworkElement { Tag: string tag } && int.TryParse(tag, out int index))
        {
            if (sender is RadioButton) Pages.SelectedIndex = index;
            else SelectPage(index);
        }
    }
    private void PageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != Pages || PageHeading == null || Pages.SelectedIndex < 0) return;
        PageHeading.Text = _pageNames[Pages.SelectedIndex];
        System.Windows.Automation.AutomationProperties.SetName(PageHeading, _pageNames[Pages.SelectedIndex]);
        StatusBadge.Visibility = Pages.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        _syncingNavigation = true;
        try
        {
            foreach (var button in NavigationItems())
                button.IsChecked = button.Tag?.ToString() == NavigationPage(Pages.SelectedIndex).ToString();
            if (SettingsNavigation != null) SettingsNavigation.IsChecked = Pages.SelectedIndex == 7;
        }
        finally { _syncingNavigation = false; }
        PageContext.Text = Pages.SelectedIndex switch
        {
            6 => "Choose an input, then choose its action",
            9 => "Your controller and saved accessories",
            8 => "Choose a setup that works for you",
            3 => "Display, reading and Windows accessibility tools",
            1 => "Make pointer movement comfortable",
            2 => "Adjust steadiness and target assistance",
            5 => "Scrolling and controller navigation",
            7 => "Make Relay feel right for you",
            4 => "Help with your next step",
            10 => "Relay by AGL Access Works",
            _ => "Your current Relay setup"
        };
        UpdateAccessibleViewport();
    }
    private void RenderRelayStates(string output)
    {
        var sample = new ControllerStatus(true, true, true, 0, "Battery medium", new Gamepad(Buttons.B, 0, 0, 0, 0, 0, 0), null, false);
        Pages.SelectedIndex = 0;
        RefreshStatus(sample); Footer.Text = "Preview state · no desktop input injected";
        CaptureRelay(output, "control-active");
        LightThemeCheck.IsChecked = false; ReadSettings();
        ToggleAppearance(this, new RoutedEventArgs());
        if (!_settings.LightTheme) throw new InvalidOperationException("Light appearance did not apply.");
        RefreshStatus(sample); Footer.Text = "Preview state · no desktop input injected";
        CaptureRelay(output, "control-light");
        Pages.SelectedIndex = 1; UpdateLayout(); CaptureRelay(output, "pointer-light");
        ToggleAppearance(this, new RoutedEventArgs());
        Pages.SelectedIndex = 0; RefreshStatus(sample with { Enabled = false });
        Footer.Text = "Preview state · no desktop input injected"; CaptureRelay(output, "control-paused");
        Pages.SelectedIndex = 1; ResponsePlot.State = DeadZonePlot.State = new Gamepad(Buttons.None, 0, 0, 20000, 8000, 0, 0);
        UpdateLayout(); CaptureRelay(output, "pointer-live");
        var target = PracticeTargets.Children.OfType<StackPanel>().First().Children.OfType<Button>().First();
        HitPracticeTarget(target, new RoutedEventArgs());
        if (_practiceHits != 1 || target.IsEnabled) throw new InvalidOperationException("Practice target failed.");
        ResetPractice(this, new RoutedEventArgs());
        Pages.SelectedIndex = 0; RefreshStatus();
    }
    private void CaptureRelay(string output, string name)
    {
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        using var file = System.IO.File.Create(System.IO.Path.Combine(output, name + ".png"));
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
    private int _practiceHits;
    private void HitPracticeTarget(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !button.IsEnabled) return;
        button.IsEnabled = false; _practiceHits++;
        PracticeScore.Text = $"{_practiceHits} of 5 targets hit";
    }
    private void ResetPractice(object sender, RoutedEventArgs e)
    {
        _practiceHits = 0; PracticeScore.Text = "0 of 5 targets hit";
        foreach (var panel in PracticeTargets.Children.OfType<StackPanel>())
            foreach (var button in panel.Children.OfType<Button>()) button.IsEnabled = true;
    }
    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OpenKeyboard(object sender, RoutedEventArgs e) { if (!_app.SmokeTest) _app.ToggleKeyboard(); }
    private void MaximizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void ToggleAppearance(object sender, RoutedEventArgs e)
    {
        LightThemeCheck.IsChecked = !(_settings.LightTheme);
        ReadSettings(updateInput: false); RefreshStatus();
        ControllerArt.InvalidateVisual(); MappingArt.InvalidateVisual();
    }
    private void WorkspaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAccessibleViewport();
    }
    private string ActionName(Shortcut action) => action switch
    {
        Shortcut.OnScreenKeyboard => "Relay keyboard",
        Shortcut.AccessibilityHub => "Accessibility hub",
        Shortcut.CloseMagnifier => "Close Magnifier",
        _ => AccessibilityCatalog.Actions.FirstOrDefault(a => a.Action == action)?.Name ?? action.ToString()
    };
    private (string Code, string Name, string Action, string Group)[] Mappings() =>
        new[] { ("LS", "Left stick", "Move pointer", "Sticks"), ("RS", "Right stick", "Scroll", "Sticks") }
        .Concat(ControlMapping.Controls.Select(c => (c.Code, c.Name, BindingName(_app.Service?.ResolveBinding(c.Control) ?? ControlMapping.Resolve(_settings, c.Control)), "Buttons & triggers")))
        .Concat((_settings.ButtonChords ?? []).Select(c => (ChordName(c), ChordName(c), BindingName(c.Action), "Combinations")))
        .Append(("Xbox", "Xbox button", "Reserved by Windows", "System")).ToArray();
    private TextBlock Label(string text, string resource, double size = 14)
    {
        var label = new TextBlock { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, resource);
        return label;
    }
    private void BuildMappingSummary()
    {
        if (MappingSummary == null || MappingDetails == null) return;
        MappingSummary.Children.Clear(); MappingDetails.Children.Clear(); _mappingRows.Clear();
        foreach (var panel in new[] { MappingSummary, MappingDetails })
        {
            string group = "";
            foreach (var item in Mappings())
            {
                if (item.Group != group)
                {
                    var title = Label(item.Group, "MutedBrush", 14); title.FontWeight = FontWeights.SemiBold;
                    title.Margin = new Thickness(0, group == "" ? 0 : 12, 0, 6);
                    panel.Children.Add(title); group = item.Group;
                }
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MinWidth = 28,
                    Padding = new Thickness(5, 2, 5, 2)
                };
                badge.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
                badge.SetResourceReference(Border.BackgroundProperty, "ControlBrush");
                var code = Label(item.Code, "TextBrush", 14); code.HorizontalAlignment = HorizontalAlignment.Center; badge.Child = code;
                grid.Children.Add(badge);
                var name = Label(item.Name, "MutedBrush", 14); Grid.SetColumn(name, 1); grid.Children.Add(name);
                name.TextWrapping = TextWrapping.Wrap; name.Margin = new Thickness(0, 0, 6, 0);
                var action = Label(item.Action, "TextBrush", 14);
                action.TextWrapping = TextWrapping.Wrap;
                action.TextAlignment = TextAlignment.Right; Grid.SetColumn(action, 2); grid.Children.Add(action);
                var row = new Border
                {
                    Child = grid,
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Transparent,
                    Padding = new Thickness(3, 3, 3, 3)
                };
                panel.Children.Add(row);
                if (panel == MappingSummary) _mappingRows[item.Code] = row;
            }
        }
    }
    private (string Code, string Name, string Action)? Describe(Gamepad pad)
    {
        var buttons = new[] { (Buttons.A,"A"),(Buttons.B,"B"),(Buttons.X,"X"),(Buttons.Y,"Y"),
            (Buttons.LeftStick,"L3"),(Buttons.RightStick,"R3"),(Buttons.LeftShoulder,"LB"),
            (Buttons.RightShoulder,"RB"),(Buttons.Back,"View"),(Buttons.Start,"Menu") };
        string? code = buttons.FirstOrDefault(b => pad.Buttons.HasFlag(b.Item1)).Item2;
        if (code == null && pad.Buttons.HasFlag(Buttons.Up)) code = "↑";
        if (code == null && pad.Buttons.HasFlag(Buttons.Down)) code = "↓";
        if (code == null && pad.Buttons.HasFlag(Buttons.Left)) code = "←";
        if (code == null && pad.Buttons.HasFlag(Buttons.Right)) code = "→";
        if (code == null && pad.LeftTrigger > 35) code = "LT";
        if (code == null && pad.RightTrigger > 35) code = "RT";
        if (code == null && InputProcessor.Shape(pad.RightX, pad.RightY, _settings.ScrollDeadZone, 1) != (0d, 0d)) code = "RS";
        if (code == null && InputProcessor.Shape(pad.LeftX, pad.LeftY, _settings.DeadZone, 1) != (0d, 0d)) code = "LS";
        if (code == null) return null;
        var entry = Mappings().First(m => m.Code == code);
        return (entry.Code, entry.Name, entry.Action);
    }
    private bool _displayedKeyboardNavigation, _displayedKeyboardSelection;
    private void RefreshRelay(ControllerStatus status, bool active)
    {
        bool keyboardNavigation = _app.Service?.KeyboardNavigationActive == true;
        bool keyboardSelection = _app.Service?.KeyboardSelectionActive == true;
        if (_displayedKeyboardNavigation != keyboardNavigation || _displayedKeyboardSelection != keyboardSelection)
        {
            _displayedKeyboardNavigation = keyboardNavigation; _displayedKeyboardSelection = keyboardSelection; BuildMappingSummary();
        }
        string badge = status.Suspended ? "Waiting for your desktop" : !status.Connected ? "Connect a controller" : !status.Enabled ? "Paused" : !status.Armed ? "Let go of the controls" : keyboardSelection ? "Keyboard control" : keyboardNavigation ? "Menu control" : "Mouse control";
        StatusBadge.Text = badge;
        StatusBadge.SetResourceReference(TextBlock.ForegroundProperty, active ? "SuccessBrush" : "MutedBrush");
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, active ? "SuccessBrush" : "ControlBrush");
        StatusGlyph.Text = active ? "▶" : status.Connected && !status.Enabled ? "Ⅱ" : "○";
        StatusGlyph.SetResourceReference(TextBlock.ForegroundProperty, active ? "OnAccentBrush" : "MutedBrush");
        StatusBanner.SetResourceReference(Border.BackgroundProperty, active ? "StatusSurfaceBrush" : "ControlBrush");
        ControllerName.Text = status.Connected ? (_app.Service?.ControllerModel ?? $"Xbox / XInput · Controller {status.Slot + 1}") : "Not connected";
        ControllerArt.Adaptive = MappingArt.Adaptive = _app.Service?.ControllerKind == ControllerKind.Adaptive;
        BatteryLabel.Text = status.Connected ? status.Battery : "USB or Bluetooth · auto connect";
        OverviewSpeed.Text = $"{_settings.PointerSpeed:0} px/s";
        ControllerArt.Connected = MappingArt.Connected = status.Connected;
        ControllerArt.Paused = MappingArt.Paused = !active;
        ControllerArt.State = MappingArt.State = status.State;
        RefreshAccessories(status.State, status.Connected);
        DeadZonePlot.State = ResponsePlot.State = status.State;
        var input = Describe(status.State);
        if (input.HasValue && status.Connected)
        {
            _lastCode = input.Value.Code; _lastPhysical = input.Value.Name; _lastRecognised = input.Value.Action; _inputAt = DateTime.UtcNow;
        }
        bool recent = status.Connected && (DateTime.UtcNow - _inputAt).TotalSeconds < 1.3;
        PhysicalInput.Text = recent ? _lastPhysical : "Nothing pressed";
        RecognisedInput.Text = !status.Connected ? "No controller" : recent ? _lastRecognised : "Waiting for input";
        WindowsOutput.Text = !active ? "Not sent · " + badge : _app.Service?.LastOutput ?? "Nothing sent";
        InputTrail.SetResourceReference(Border.BorderBrushProperty, recent && active ? "InputBrush" : "LineBrush");
        ControllerArt.CalloutCode = MappingArt.CalloutCode = recent ? _lastCode : "";
        ControllerArt.Callout = MappingArt.Callout = recent ? (active ? _lastCode + " · " + _lastRecognised : _lastCode + " · not sent") : "";
        foreach (var row in _mappingRows)
        {
            row.Value.BorderBrush = recent && row.Key == _lastCode ? (Brush)FindResource(active ? "InputBrush" : "MutedBrush") : Brushes.Transparent;
        }
    }
}

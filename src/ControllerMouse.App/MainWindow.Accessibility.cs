using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private double InterfaceTextScale => VisualTextScale.Resolve(_settings.InterfaceScale, _app.WindowsTextScale);
    internal void RefreshSystemTextScale()
    {
        UpdateAccessibleViewport();
        UpdateSystemTextSizeNotice();
        _lastPresentationSettings = null;
        RefreshStatus();
    }
    internal bool HoverReadingEnabled => _settings.ReadHoveredText;
    private void InitializeAccessibility()
    {
        StarterProfile.ItemsSource = AccessibilityProfiles.All.Append(AccessibilityProfiles.NavigationTest).ToArray();
        StarterProfile.SelectedIndex = 0;
        AccessibilitySupport.EnableKeyboardAccess(this);
        AccessibilitySupport.Heading(PageHeading);
        foreach (var text in new[] { StatusDetail, DisconnectedInstructionText, StatusBadge, Footer, MappingNotice, ProfileNotice, PracticeScore, MotionNotice, ChordNotice, StarterDescription })
            AccessibilitySupport.AnnounceChanges(text);
        foreach (var (slider, units) in new[] {
            (SpeedSlider, "Pixels per second."), (DpadSpeedSlider, "Pixels per second."), (DeadZoneSlider, "Percent of stick travel."),
            (ScrollDeadZoneSlider, "Percent of stick travel."), (CurveSlider, "100 means linear; 300 means a response exponent of 3."),
            (ScrollCurveSlider, "100 means linear; 300 means a response exponent of 3."),
            (SmoothingSlider, "Milliseconds."), (ScrollSmoothingSlider, "Milliseconds."), (HoldSlider, "Milliseconds; zero means immediate clicks."),
            (StabilizationSlider, "Percent."), (TargetSlider, "Percent."), (HapticStrengthSlider, "Percent."), (ScrollSlider, "Wheel notches per second.") })
        {
            AutomationProperties.SetHelpText(slider, units + " Use arrow keys for small changes, Page Up or Page Down for larger changes, Home or End for the limits.");
            void UpdateValue() => AutomationProperties.SetItemStatus(slider,
                slider == CurveSlider || slider == ScrollCurveSlider ? $"Response exponent {slider.Value / 100:0.00}." :
                slider == HoldSlider && slider.Value == 0 ? "Immediate click. No extra hold required." : $"{slider.Value:0} {units.ToLowerInvariant()}");
            slider.ValueChanged += (_, _) => UpdateValue(); UpdateValue();
        }
        AutomationProperties.SetHelpText(ChordWindowSlider, "Milliseconds allowed between the first and second button. Longer windows make combinations easier, but delay individual actions on those buttons.");
        void ChordValue() => AutomationProperties.SetItemStatus(ChordWindowSlider, $"{ChordWindowSlider.Value:0} milliseconds.");
        ChordWindowSlider.ValueChanged += (_, _) => ChordValue(); ChordValue();
        foreach (var radio in NavigationItems())
        {
            int index = int.Parse(radio.Tag.ToString()!);
            AutomationProperties.SetAutomationId(radio, "Navigate_" + _pageNames[index]);
            AutomationProperties.SetAcceleratorKey(radio, index == 9 ? "Ctrl+Alt+H" : index == 10 ? "" : $"Ctrl+{index + 1}");
            AutomationProperties.SetHelpText(radio, "D-pad Up or Down chooses a page. Right opens its heading; Down enters its controls. RB moves to the next control; LB goes back. A selects; B returns to pages. Sliders and text fields use their arrow keys. F6 moves between pages and controls.");
        }
        ZoomCombo.SelectedIndex = (int)Math.Round((_settings.InterfaceScale - 1) * 4);
        Loaded += (_, _) =>
        {
            Width = Math.Min(Width, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height);
            UpdateAccessibleViewport();
            if (_app.Profiles.SetupCompleted) AccessibilitySupport.FocusHeading(PageHeading);
        };
        AccessViewport.SizeChanged += (_, _) => UpdateAccessibleViewport();
        AccessViewport.ScrollChanged += (_, e) =>
        {
            if (e.Source == AccessViewport && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) UpdateAccessibleViewport();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down &&
                Keyboard.FocusedElement is RadioButton item && NavigationItems().Contains(item))
            {
                NavigateController(e.Key == Key.Down ? RelayNavigationCommand.Down : RelayNavigationCommand.Up);
                e.Handled = true; return;
            }
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F6)
            { FocusPageOrNavigation(); e.Handled = true; return; }
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.F12)
            { _app.OpenDiagnostics(); e.Handled = true; return; }
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.H)
            { SelectPage(9); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            { SelectPage((int)e.Key - (int)Key.D1); e.Handled = true; return; }
            int index = ZoomCombo.SelectedIndex;
            if (e.Key is Key.Add or Key.OemPlus) index++;
            else if (e.Key is Key.Subtract or Key.OemMinus) index--;
            else if (e.Key is Key.D0 or Key.NumPad0) index = 0;
            else return;
            ZoomCombo.SelectedIndex = Math.Clamp(index, 0, 4); e.Handled = true;
        };
    }
    private void UpdateAccessibleViewport()
    {
        if (Workspace == null || AccessViewport == null || SidebarColumn == null) return;
        double scale = InterfaceTextScale;
        Workspace.LayoutTransform = Transform.Identity;
        Workspace.Width = Workspace.Height = double.NaN;
        FontSize = 16 * scale;
        foreach (var (key, size) in new[] { ("MainFontSize", 16d), ("CaptionFontSize", 14d), ("PageTitleFontSize", 32d), ("SectionFontSize", 20d), ("ImportantActionFontSize", 18d) })
            if (!Equals(Application.Current.Resources[key], size * scale)) Application.Current.Resources[key] = size * scale;
        double targetHeight = _settings.EffectiveVisualAccess.Spacing switch { VisualSpacing.Comfortable => 50, VisualSpacing.Wide => 56, _ => 44 };
        Application.Current.Resources["ControlMinHeight"] = targetHeight * scale;
        Application.Current.Resources["ControlPadding"] = _settings.EffectiveVisualAccess.Spacing switch
        {
            VisualSpacing.Comfortable => new Thickness(16, 13, 16, 13),
            VisualSpacing.Wide => new Thickness(20, 18, 20, 18),
            _ => new Thickness(12, 10, 12, 10)
        };
        bool compactNavigation = scale > 1 && ActualWidth < 900;
        double navigationWidth = scale >= 1.5 ? 240 : 210;
        if (scale > 1)
        {
            double labelWidth = NavigationItems().Max(radio => new FormattedText(radio.Content?.ToString() ?? "",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection,
                new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                16 * scale, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace);
            // Reserve the rail margins, focus frame, label inset and a full-size scrollbar.
            navigationWidth = Math.Max(navigationWidth, Math.Ceiling(labelWidth + 82 + (compactNavigation ? 0 : 32)));
        }
        SidebarColumn.Width = new GridLength(navigationWidth);
        foreach (var radio in NavigationItems())
        {
            radio.ApplyTemplate();
            if (radio.Template.FindName("NavIconColumn", radio) is ColumnDefinition iconColumn)
                iconColumn.Width = new GridLength(compactNavigation ? 0 : 32);
            if (radio.Template.FindName("NavGlyph", radio) is FrameworkElement glyph)
                glyph.Visibility = compactNavigation ? Visibility.Collapsed : Visibility.Visible;
            if (radio.Template.FindName("NavLabel", radio) is TextBlock label)
                label.Margin = compactNavigation ? new Thickness(8, 0, 0, 0) : new Thickness(0);
        }
        double width = AccessViewport.ActualWidth;
        foreach (string prefix in new[] { "Control", "Mapping", "Hardware" })
        {
            if (FindName(prefix + "Workbench") is not Grid workbench || FindName(prefix + "VisualPane") is not FrameworkElement visual ||
                FindName(prefix + "InspectorPane") is not FrameworkElement inspector) continue;
            bool stacked = width < 700 + 330 * scale || _portsView && prefix != "Mapping";
            if (FindName(prefix + "InspectorColumn") is ColumnDefinition column) column.Width = new GridLength(stacked ? 0 : 330 * scale);
            if (workbench.ColumnDefinitions.Count > 1) workbench.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 24);
            Grid.SetColumn(visual, 0); Grid.SetRow(visual, 0); Grid.SetColumnSpan(visual, stacked ? 3 : 1);
            Grid.SetColumn(inspector, stacked ? 0 : 2); Grid.SetRow(inspector, stacked ? 1 : 0); Grid.SetColumnSpan(inspector, stacked ? 3 : 1);
            inspector.Margin = new Thickness(0, stacked ? 20 : 0, 0, 0);
        }
        ScaleLegacyText(scale);
        ReflowSecondaryPages(width, scale);
        foreach (var tile in _controlTiles.Values.Concat(_hardwareTiles.Values)) tile.Button.Width = Math.Min(112 * scale, Math.Max(44, width - 64));
        double usablePageWidth = Math.Max(44, width - 48 - 44); // Page padding and accessible scrollbar.
        foreach (var button in MappingChoicesHost?.Children.OfType<Button>() ?? []) button.Width = Math.Min(220 * scale, Math.Max(44, usablePageWidth - 8));
        UpdateSystemTextSizeNotice();
    }
    private void ZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ZoomCombo.SelectedIndex < 0 || Workspace == null) return;
        _settings = _settings with { InterfaceScale = ZoomCombo.SelectedItem is ComboBoxItem { Tag: double exact } ? exact : 1 + ZoomCombo.SelectedIndex * .25 };
        UpdateAccessibleViewport();
        if (!_loading) { ReadSettings(updateInput: false); SaveSettings(); }
    }
    private void StarterProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StarterDescription != null && StarterProfile.SelectedItem is AccessibilityPreset preset)
            StarterDescription.Text = preset.Description;
    }
    private void AddStarterProfile(object sender, RoutedEventArgs e)
    {
        if (StarterProfile.SelectedItem is not AccessibilityPreset preset) return;
        var settings = preset.Settings;
        if (settings.ControllerType == ControllerPreference.Adaptive)
            settings = settings with { AdaptiveAccessories = _settings.AdaptiveAccessories };
        if (AddProfile(new(Guid.NewGuid().ToString("N"), preset.Name, settings)))
            ProfileNotice.Text = $"{_app.Profiles.Active.Name} is ready. Release the controls, then try it. Choose Mapping to read every button assignment.";
    }

    private System.Collections.Generic.IEnumerable<RadioButton> NavigationItems() => NavigationButtons.Children.OfType<RadioButton>().Append(HelpNavigation).Append(AboutNavigation).Distinct();
    private RadioButton CurrentNavigation() => NavigationItems()
        .First(button => button.Tag?.ToString() == NavigationPage(Pages.SelectedIndex).ToString());
    private void SelectPage(int index)
    {
        Pages.SelectedIndex = index; UpdateLayout(); AccessibilitySupport.FocusHeading(PageHeading);
    }
    private void FocusPageOrNavigation()
    {
        if (NavigationButtons.IsKeyboardFocusWithin || HelpNavigation.IsKeyboardFocused || AboutNavigation.IsKeyboardFocused)
            AccessibilitySupport.FocusHeading(PageHeading);
        else { var navigation = CurrentNavigation(); navigation.BringIntoView(); Keyboard.Focus(navigation); }
    }
    private void RunAccessibilityChecks(string output)
    {
        foreach (var mode in Enum.GetValues<LearningMode>())
        {
            MotionLearningCombo.SelectedIndex = (int)mode;
            if (_settings.MotionLearning != mode) throw new InvalidOperationException("Motion learning mode binding failed.");
        }
        MotionLearningCombo.SelectedIndex = 0;
        ForgetMotion(this, new RoutedEventArgs());
        int count = 0;
        foreach (var radio in NavigationItems())
        {
            ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(radio)!.GetPattern(PatternInterface.SelectionItem)).Select();
            if (Pages.SelectedIndex.ToString() != radio.Tag.ToString()) throw new InvalidOperationException("Accessible page selection failed.");
        }
        static System.Collections.Generic.IEnumerable<DependencyObject> Elements(DependencyObject parent)
        {
            yield return parent;
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
                foreach (var descendant in Elements(child)) yield return descendant;
        }
        for (int page = 0; page < Pages.Items.Count; page++)
        {
            Pages.SelectedIndex = page; UpdateLayout();
            foreach (var element in Elements((DependencyObject)((TabItem)Pages.Items[page]).Content))
            {
                if (element is not System.Windows.Controls.Control control || !control.Focusable || !control.IsEnabled ||
                    control is not (System.Windows.Controls.Primitives.ButtonBase or ComboBox or Slider or TextBox or ListBox)) continue;
                var peer = UIElementAutomationPeer.CreatePeerForElement(control);
                if (peer == null || string.IsNullOrWhiteSpace(peer.GetName())) throw new InvalidOperationException($"Missing accessible name: {control.GetType().Name} {control.Name}, page {page}.");
                var pattern = control switch
                {
                    Slider => PatternInterface.RangeValue,
                    TextBox => PatternInterface.Value,
                    ComboBox or ListBox => PatternInterface.Selection,
                    CheckBox => PatternInterface.Toggle,
                    _ => PatternInterface.Invoke
                };
                if (peer.GetPattern(pattern) == null) throw new InvalidOperationException($"Missing {pattern} for {peer.GetName()}.");
                if (control is ComboBox && peer.GetPattern(PatternInterface.ExpandCollapse) == null)
                    throw new InvalidOperationException($"Cannot open combo box: {peer.GetName()}.");
                count++;
            }
        }
        Pages.SelectedIndex = 8; StarterProfile.SelectedIndex = 2; AddStarterProfile(this, new RoutedEventArgs());
        if (_settings.ScrollMode != ScrollMode.Step || !_settings.ScrollPrecision) throw new InvalidOperationException("Starter profile UI failed.");
        ZoomCombo.SelectedIndex = 4; Width = 640; Height = 480; UpdateLayout(); UpdateAccessibleViewport(); UpdateLayout();
        var profileViewport = (ScrollViewer)((TabItem)Pages.Items[8]).Content;
        if (_settings.InterfaceScale != 2 || profileViewport.ScrollableHeight <= 0 || AccessViewport.ScrollableWidth > 1)
            throw new InvalidOperationException("Zoomed viewport cannot reach all content.");
        ProfileName.BringIntoView(); UpdateLayout(); CaptureRelay(output, "accessibility-200-percent");
        ZoomCombo.SelectedIndex = 0; Width = 1280; Height = 860; UpdateLayout();
        AccessViewport.ScrollToHome();
        _app.Profiles = ProfileLibrary.Create(new Settings()); _settings = new Settings(); PopulateControls(); PopulateProfiles();
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "accessibility.txt"),
            $"PASS: main pages selected through UI Automation; {count} controls have accessible names and expected action/value patterns; starter profile creation; 200% text at 640x480 has vertical scrolling without whole-page horizontal scrolling. Narrator/NVDA, braille, switch access and mixed-DPI hardware still require manual acceptance testing.");
    }
    private readonly System.Collections.Generic.Dictionary<DependencyObject, double> _legacyTextSizes = new();
    private void ScaleLegacyText(double scale)
    {
        if (Pages == null) return;
        void Visit(DependencyObject element)
        {
            if (element.ReadLocalValue(TextBlock.FontSizeProperty) is double size && element is TextBlock or System.Windows.Controls.Control)
            {
                if (!_legacyTextSizes.TryGetValue(element, out double original)) _legacyTextSizes[element] = original = size;
                element.SetValue(TextBlock.FontSizeProperty, Math.Max(14, original) * scale);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) Visit(child);
        }
        foreach (var page in Pages.Items.OfType<TabItem>()) if (page.Content is DependencyObject content) Visit(content);
    }
}

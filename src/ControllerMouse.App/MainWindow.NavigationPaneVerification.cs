using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunNavigationPaneChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Pane navigation verification requires isolated Relay without desktop output.");
        var settings = _settings;
        int originalPage = Pages.SelectedIndex;
        double width = Width, height = Height;
        var windowState = WindowState;
        bool ports = _portsView;
        string selectedInput = _selectedInput;
        var sectionStates = Pages.Items.OfType<TabItem>().SelectMany(item => NavigationInteractionDescendants(item))
            .OfType<Expander>().Distinct().ToDictionary(section => section, section => section.IsExpanded);
        var sink = new NavigationVerificationSink();
        var commands = new List<RelayNavigationCommand>();
        var focusChanges = new List<IInputElement>();
        var report = new StringBuilder();
        var engine = new InputProcessor(sink)
        {
            Settings = new Settings { NarratorNavigation = true, ClickHoldMilliseconds = 0, Smoothing = 0 },
            RelayWindowActive = true,
            RelayNavigationRequested = command =>
            {
                commands.Add(command);
                Dispatcher.BeginInvoke(new Action(() => NavigateController(command)));
            }
        };
        KeyboardFocusChangedEventHandler changed = (_, e) => { if (e.NewFocus != null) focusChanges.Add(e.NewFocus); };
        AddHandler(Keyboard.GotKeyboardFocusEvent, changed, true);
        engine.Process(default(Gamepad), .008);
        Task Drain() => Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        string FocusName() => Keyboard.FocusedElement is DependencyObject element
            ? $"{element.GetType().Name} '{AutomationProperties.GetName(element)}'" : "no keyboard focus";
        void Check(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException($"{detail}; page {_pageNames[Pages.SelectedIndex]}, focus {FocusName()}, " +
                $"commands [{string.Join(", ", commands)}], actual focus events {focusChanges.Count}.");
        }
        async Task Press(Buttons button, int frames = 1)
        {
            commands.Clear(); focusChanges.Clear();
            for (int frame = 0; frame < frames; frame++) engine.Process(new Gamepad(button, 0, 0, 0, 0, 0, 0), .008);
            engine.Process(default(Gamepad), .008);
            await Drain();
            Check(commands.Count == 1 && focusChanges.Count <= 1, "A controller edge caused missing/duplicate commands or transient focus");
        }
        Button FindButton(DependencyObject root, string name) => NavigationInteractionDescendants(root).OfType<Button>()
            .Single(button => AutomationProperties.GetName(button) == name || button.Content?.ToString() == name);
        void Focus(UIElement control)
        {
            if (control is FrameworkElement element) element.BringIntoView();
            Check(control.Focus(), "An actual test target cannot receive keyboard focus");
        }
        var preview = new ControllerStatus(true, true, true, 0, "Battery medium", default, null, false);
        int pageCases = 0, reached = 0;
        try
        {
            WindowState = WindowState.Normal;
            SetPortsView(false);
            foreach (var size in new[] { new Size(1440, 900), new Size(640, 480) })
                foreach (double scale in new[] { 1d, 2d })
                {
                    Width = size.Width; Height = size.Height;
                    _settings = settings with
                    {
                        ControllerType = ControllerPreference.Adaptive,
                        InterfaceScale = scale,
                        VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
                    };
                    for (int page = 0; page < Pages.Items.Count; page++)
                    {
                        SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                        var content = (ScrollViewer)((TabItem)Pages.Items[page]).Content;
                        content.ScrollToHome(); UpdateLayout();
                        UIElement expected = page switch
                        {
                            0 => _frontButtons.Single(button => AutomationProperties.GetAutomationId(button) == "FrontView_ControlViewsHost"),
                            1 => DeadZoneSlider,
                            2 => MotionLearningCombo,
                            3 => _themeChoice!.ChoiceButtons[0],
                            4 => FindButton(content, "Change my setup"),
                            5 => ScrollModeCombo,
                            6 => ActionCategoriesHost.Children.OfType<Button>().First(),
                            7 => FindButton(content, "Set up Relay for me"),
                            8 => ProfileList.Children.OfType<Button>().First(),
                            9 => _frontButtons.Single(button => AutomationProperties.GetAutomationId(button) == "FrontView_HardwareViewsHost"),
                            10 => FindButton(content, "Open Accessibility"),
                            _ => throw new InvalidOperationException("Unexpected page.")
                        };
                        AccessibilitySupport.FocusHeading(PageHeading); await Drain();
                        await Press(Buttons.Down, 125);
                        Check((expected.IsKeyboardFocused || expected is ListBox && expected.IsKeyboardFocusWithin) && focusChanges.Count == 1 && Pages.SelectedIndex == page,
                            "One held Down from the page heading did not enter the independently specified first page action");
                        await Press(Buttons.B);
                        Check(CurrentNavigation().IsKeyboardFocused && focusChanges.Count == 1 && Pages.SelectedIndex == page,
                            "Back from page content did not return to its sidebar without changing page");
                        await Press(Buttons.Right);
                        Check(PageHeading.IsKeyboardFocused && focusChanges.Count == 1, "Sidebar Right did not focus just the selected page heading");
                        await Press(Buttons.RightShoulder);
                        Check((expected.IsKeyboardFocused || expected is ListBox && expected.IsKeyboardFocusWithin) && focusChanges.Count == 1, "RB from the heading drifted into shell chrome or a hidden TabItem");
                        await Press(Buttons.LeftShoulder);
                        Check(PageHeading.IsKeyboardFocused && focusChanges.Count == 1, "LB from the first page control did not return to the heading");
                        await Press(Buttons.Up);
                        Check(CurrentNavigation().IsKeyboardFocused && focusChanges.Count == 1, "Heading Up did not return to pages");
                        pageCases++;
                        report.AppendLine($"PASS {_pageNames[page]}, {size.Width:0}×{size.Height:0}, {scale:P0}: heading Down/RB enter its first actual action, LB returns heading, Up/B return sidebar; one focus event per edge.");
                    }
                }

            // Traverse whole pages without pre-focusing any pane. These independently
            // named controls cover the drawing, inspector, settings and later panels.
            foreach (var context in new[] { (Size: new Size(1440, 900), Scale: 1d), (Size: new Size(640, 480), Scale: 2d) })
            {
                Width = context.Size.Width; Height = context.Size.Height;
                _settings = settings with
                {
                    ControllerType = ControllerPreference.Standard,
                    InterfaceScale = context.Scale,
                    VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
                };
                foreach (int page in Enumerable.Range(0, Pages.Items.Count))
                {
                    SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                    var root = (ScrollViewer)((TabItem)Pages.Items[page]).Content;
                    UIElement[] mustReach = page switch
                    {
                        0 => [LiveSurface, _inspectors[0].Change, ControlTilesHost, PauseButton, ControlChangeSetup],
                        1 => [DeadZoneSlider, SpeedSlider, CurveSlider, SmoothingSlider],
                        2 => [MotionLearningCombo, TremorCheck, TargetCheck],
                        3 => [_themeChoice!.ChoiceButtons[0], _textSizeChoice!.ChoiceButtons[0], _spacingChoice!.ChoiceButtons[0], _complexityChoice!.ChoiceButtons[0], _focusChoice!.ChoiceButtons[0], _oneControlChoice!, _moreVisualOptions!, ReadHoveredTextCheck, ReadingFeedbackOptions, WindowsToolsOptions],
                        4 => [FindButton(root, "Change my setup"), FindButton(root, "Make Relay easier to see"), FindButton(root, "Open local app data")],
                        5 => [ScrollModeCombo, ScrollSlider, NarratorNavigationCheck, HapticsCheck],
                        6 => [ActionCategoriesHost.Children.OfType<Button>().First(), MappingSurface, _inspectors[1].Change, MappingChoicesHost.Children.OfType<Button>().First()],
                        7 => [FindButton(root, "Set up Relay for me"), LightThemeCheck, ZoomCombo, FindButton(root, "Open Visual Access"), TrayCheck, StartupCheck, FindButton(root, "Quit Relay")],
                        8 => [ProfileList.Children.OfType<Button>().First(), ProfileName, StarterProfile],
                        9 => [HardwareSurface, _hardwareLayout!, FindButton(HardwareDeviceHost, "Reconnect"), FindButton(HardwareDeviceHost, "Identify an input"), HardwareInputsHost],
                        10 => [FindButton(root, "Open Accessibility"), FindButton(root, "Read Relay help")],
                        _ => throw new InvalidOperationException("Unexpected page.")
                    };
                    var visited = new HashSet<UIElement>();
                    AccessibilitySupport.FocusHeading(PageHeading);
                    for (int step = 0; step < 350; step++)
                    {
                        await Press(Buttons.RightShoulder);
                        Check(Keyboard.FocusedElement is UIElement { IsVisible: true, IsEnabled: true } && Keyboard.FocusedElement is not TabItem,
                            "RB reached hidden, disabled or tab-header content");
                        var target = (UIElement)Keyboard.FocusedElement;
                        if (ReferenceEquals(target, CurrentNavigation())) break;
                        Check(NavigationContains(root, target) || NavigationContains(PageHeaderActions, target) || target == ProfileCombo,
                            "Predictable page traversal escaped into an unrelated page or caption button");
                        Check(visited.Add(target), "Page traversal repeated a focus target before reaching its sidebar endpoint");
                    }
                    bool Reached(UIElement required) => visited.Any(target => NavigationContains(required, target));
                    Check(CurrentNavigation().IsKeyboardFocused && mustReach.All(Reached), "RB could not reach every independently specified page/pane action: " +
                        string.Join(", ", mustReach.Where(control => !Reached(control)).Select(control => control.GetType().Name + " " +
                            (control is FrameworkElement element ? element.Name + " " : "") + AutomationProperties.GetName(control) + "; enabled " + control.IsEnabled)));
                    Check(NavigationControls(PageHeaderActions).All(visited.Contains) && visited.Contains(ProfileCombo), "Keyboard, appearance or active profile was omitted from the explicit order");
                    reached += visited.Count;
                    report.AppendLine($"PASS {_pageNames[page]}, {context.Size.Width:0}×{context.Size.Height:0}, {context.Scale:P0}, complete RB traversal: {visited.Count} distinct actual controls, including all expected panes and named header/profile actions; no hidden page or shell drift.");
                }
            }

            foreach (var context in new[] { (Size: new Size(1440, 900), Scale: 1d), (Size: new Size(640, 480), Scale: 2d) })
            {
                Width = context.Size.Width; Height = context.Size.Height;
                _settings = settings with
                {
                    ControllerType = ControllerPreference.Standard,
                    InterfaceScale = context.Scale,
                    VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
                };
                SelectPage(6); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                Focus(ActionCategoriesHost.Children.OfType<Button>().First());
                await Press(Buttons.Right);
                Check(Keyboard.FocusedElement is DependencyObject drawing && NavigationContains(MappingSurface, drawing), "Mapping Right did not cross from action categories into the actual controller drawing");
                await Press(Buttons.RightShoulder);
                Check(Keyboard.FocusedElement is DependencyObject inspector && NavigationContains(MappingInspectorPane, inspector), "Mapping RB did not leave the drawing group for its inspector");
                await Press(Buttons.Left);
                Check(Keyboard.FocusedElement is DependencyObject returned && NavigationContains(MappingSurface, returned), "Mapping Left did not return from inspector to the drawing");
                report.AppendLine($"PASS Mapping panes, {context.Size.Width:0}×{context.Size.Height:0}/{context.Scale:P0}, category row {Grid.GetRow(ActionCategoriesHost)}, drawing row {Grid.GetRow(MappingCanvasPane)}: category Right → controller group RB → inspector action Left → controller group, one focus event per edge.");
            }

            Width = 1440; Height = 900;
            _settings = settings with
            {
                ControllerType = ControllerPreference.Standard,
                InterfaceScale = 1,
                VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
            };
            SelectPage(7); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
            Focus(FindButton((ScrollViewer)Pages.SelectedContent, "Set up Relay for me"));
            await Press(Buttons.Down);
            Check(LightThemeCheck.IsKeyboardFocused, "Settings Down from setup did not reach the appearance checkbox");
            await Press(Buttons.Up);
            Check(Keyboard.FocusedElement is Button setupButton && setupButton.Content?.ToString() == "Set up Relay for me", "Settings Up did not reverse its logical read order");
            await Press(Buttons.Up);
            Check(PageHeading.IsKeyboardFocused, "First-control Up did not return to the heading");
            // The last Down clamps, while RB explicitly continues to header actions.
            var quit = FindButton((ScrollViewer)Pages.SelectedContent, "Quit Relay");
            Focus(quit); await Press(Buttons.Down, 125);
            Check(quit.IsKeyboardFocused && focusChanges.Count == 0, "Last-control Down did not clamp its page endpoint");
            report.AppendLine("PASS Settings vertical read order and endpoints: setup Down → appearance, Up → setup, Up → heading; held Down on Quit remains clamped without activating it.");

            Width = 640; Height = 480;
            _settings = settings with
            {
                ControllerType = ControllerPreference.Standard,
                InterfaceScale = 2,
                VisualAccess = settings.EffectiveVisualAccess with { OneControlAtATime = false, Complexity = VisualComplexity.Standard }
            };
            SelectPresentedInput("A");
            ToggleButton SectionHeader(Expander section) => LayoutVisualElements(section).OfType<ToggleButton>()
                .First(toggle => ReferenceEquals(FindControl<Expander>(toggle), section));
            foreach (int page in new[] { 6, 9 })
            {
                SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                var content = (ScrollViewer)Pages.SelectedContent;
                var section = page == 6 ? NavigationInteractionDescendants(content).OfType<Expander>()
                    .Single(expander => expander.Header?.ToString() == "Button, trigger and keyboard shortcut options") : HardwareInputDetails;
                section.IsExpanded = false; UpdateLayout();
                var header = SectionHeader(section);
                Focus(header); await Press(Buttons.A);
                Check(section.IsExpanded && header.IsKeyboardFocused && Pages.SelectedIndex == page,
                    "Controller A did not expand the actual section while preserving its header focus");
                UpdateLayout(); await Press(Buttons.RightShoulder);
                Check(page == 6 ? MappingControl.IsKeyboardFocused : _inspectors[2].Change.IsKeyboardFocused,
                    "RB after expanding a section did not enter its actual first advanced/detail action");
                await Press(Buttons.B);
                Check(CurrentNavigation().IsKeyboardFocused && Pages.SelectedIndex == page && section.IsExpanded,
                    "Back from an expanded section failed to return to pages or discarded its expansion");
                report.AppendLine($"PASS {_pageNames[page]} at 640×480/200%: A expands its actual header, RB enters its first advanced/detail field, B returns to pages with the section preserved.");
            }

            int sectionDestinations = 0;
            foreach (bool expanded in new[] { false, true })
                foreach (int page in Enumerable.Range(0, Pages.Items.Count))
                {
                    SelectPage(page); UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                    var content = (ScrollViewer)Pages.SelectedContent;
                    var sections = NavigationInteractionDescendants(content).OfType<Expander>().ToArray();
                    foreach (var section in sections) section.IsExpanded = expanded;
                    UpdateLayout();
                    // Ordinary fields remain exhaustive. Duplicate input members have
                    // one region entry and are independently explored in the group fixture.
                    var required = NavigationInteractionDescendants(content).OfType<System.Windows.Controls.Control>()
                        .Where(control => control.IsVisible && control.IsEnabled && control.Focusable &&
                            control is ButtonBase or ComboBox or Slider or TextBox or ListBox)
                        .Where(control => !IsGroupedNavigationMember(control) || KeyboardNavigation.GetIsTabStop(control)).Cast<UIElement>()
                        .Concat(sections.Where(section => section.IsVisible).Select(SectionHeader)).ToArray();
                    var visited = new HashSet<UIElement>();
                    AccessibilitySupport.FocusHeading(PageHeading);
                    for (int step = 0; step < 600; step++)
                    {
                        await Press(Buttons.RightShoulder);
                        Check(Keyboard.FocusedElement is UIElement { IsVisible: true, IsEnabled: true } && Keyboard.FocusedElement is not TabItem,
                            "Expanded/collapsed traversal reached hidden or disabled content");
                        var target = (UIElement)Keyboard.FocusedElement;
                        if (ReferenceEquals(target, CurrentNavigation())) break;
                        Check(NavigationContains(content, target) || NavigationContains(PageHeaderActions, target) || target == ProfileCombo,
                            "Expanded/collapsed traversal escaped its selected page");
                        Check(visited.Add(target), "Expanded/collapsed traversal repeated a target before reaching pages");
                    }
                    var missing = required.Where(control => !visited.Any(target => NavigationContains(control, target))).ToArray();
                    Check(CurrentNavigation().IsKeyboardFocused && missing.Length == 0,
                        "Expanded/collapsed traversal omitted actual visible logical controls: " + string.Join(", ", missing.Select(control =>
                            control.GetType().Name + " " + AutomationProperties.GetName(control))));
                    sectionDestinations += visited.Count;
                    report.AppendLine($"PASS {_pageNames[page]}, all sections {(expanded ? "expanded" : "collapsed")}, 640×480/200%: {visited.Count} distinct focus targets; every independently inventoried visible field/action and section header reached.");
                }

            SelectPage(0); RefreshStatus(preview with { Connected = false }); UpdateLayout();
            AccessibilitySupport.FocusHeading(PageHeading);
            await Press(Buttons.Down);
            Check(Keyboard.FocusedElement is Button hardware && hardware.Content?.ToString() == "Open Hardware", "Disconnected Control heading did not enter its visible connection action");
            Keyboard.ClearFocus(); await Press(Buttons.Down);
            Check(CurrentNavigation().IsKeyboardFocused && focusChanges.Count == 1, "Missing keyboard focus silently discarded the controller edge instead of recovering once to pages");
            report.AppendLine("PASS disconnected/empty focus: heading enters Open Hardware; a command after clearing keyboard focus recovers once to the current sidebar without advancing twice.");
            Check(sink.Keys.Count == 0 && sink.Shortcuts.Count == 0 && sink.MouseChanges.Count == 0 && sink.MoveCalls == 0,
                "Page and pane navigation emitted native keyboard/pointer output");
            report.Insert(0, $"PASS {pageCases} actual page/size/scale heading-entry cases, {reached} focus destinations across {Pages.Items.Count * 2} complete page/group traversals (1440×900/100% and 640×480/200%), and {sectionDestinations} destinations across {Pages.Items.Count * 2} collapsed/expanded section traversals at 200%. Duplicate input members have one region entry and separate complete member checks. Commands reach the real WPF handler through the controller engine. Focus transitions, field entry, panes, endpoints, hidden-page exclusion and focus recovery are checked without native output. Narrator listening and physical controller acceptance remain manual.\n\n");
            File.WriteAllText(Path.Combine(output, "controller-navigation-panes.txt"), report.ToString());
        }
        finally
        {
            RemoveHandler(Keyboard.GotKeyboardFocusEvent, changed); engine.Reset();
            _settings = settings; Width = width; Height = height; WindowState = windowState;
            foreach (var (section, expanded) in sectionStates) section.IsExpanded = expanded;
            SetPortsView(ports); SelectPresentedInput(selectedInput); SelectPage(originalPage); UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
            await Drain();
        }
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;
using Microsoft.Win32;

namespace ControllerMouse.App;



public partial class MainWindow : Window

{

    private readonly App _app;

    private Settings _settings;

    private bool _loading = true, _notified, _hotkeyAvailable = true;

    private string? _saveError;

    private HwndSource? _source;

    private readonly TargetOverlay? _overlay;

    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(33) };

    private readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(350) };

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string StartupName = "XboxControllerMouse";



    internal MainWindow(App app, Settings settings)

    {

        _app = app; _settings = settings;

        InitializeComponent();

        PopulateControls(); BuildGuide(); BuildAccessibility(); BuildMappingSummary();

        PopulateProfiles(); InitializeMappingEditor();

        InitializeChordEditor();

        InitializeAccessibility();

        InitializePresentation();

        InitializeVisualAccess();
        InitializeNavigationGroups();
        Loaded += (_, _) => RefreshNavigationGroups();
        AboutVersionText.Text = "Version " + (typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "Public beta");

        _overlay = app.SmokeTest ? null : new TargetOverlay();

        SourceInitialized += OnSourceInitialized;

        Closing += OnClosing;

        Closed += (_, _) =>

        {

            _refresh.Stop(); _save.Stop(); _overlay?.Close();

            if (_source != null) { NativeMethods.UnregisterHotKey(_source.Handle, 9001); _source.RemoveHook(WindowHook); }

        };

        _refresh.Tick += (_, _) => RefreshStatus();

        _save.Tick += (_, _) => { _save.Stop(); SaveSettings(); };

        if (!app.SmokeTest) _refresh.Start();

        _loading = false;

        UpdateLabels(); RefreshStatus();

    }



    private void OnSourceInitialized(object? sender, EventArgs e)

    {

        var handle = new WindowInteropHelper(this).Handle;

        // Attribute 20 is documented from Windows 11; older Windows keeps its own frame.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int dark = _settings.EffectiveVisualAccess.IsDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        }

        _source = HwndSource.FromHwnd(handle);

        _navigationPointerKnown = NativeMethods.GetPhysicalCursorPos(out _lastNavigationPointer);

        _source?.AddHook(WindowHook);

        if (_app.SmokeTest) return;

        _hotkeyAvailable = NativeMethods.RegisterHotKey(handle, 9001, 0x4003, 0x50);

    }



    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)

    {

        ObservePointerMessage(message);

        if (message == 0x0312 && wParam.ToInt32() == 9001) { _app.Service?.Toggle(); handled = true; RefreshStatus(); }

        return IntPtr.Zero;

    }



    private void PopulateControls()

    {

        _loading = true;

        SpeedSlider.Value = _settings.PointerSpeed;

        DpadSpeedSlider.Value = _settings.DpadSpeed;

        ScrollSlider.Value = _settings.ScrollSpeed;

        ScrollModeCombo.ItemsSource = new[] { new ScrollModeOption(ScrollMode.Smooth, "Smooth · fine wheel increments"),

            new ScrollModeOption(ScrollMode.Compatible, "Compatible · full wheel notches"), new ScrollModeOption(ScrollMode.Step, "Step · one notch per nudge") };

        ScrollModeCombo.SelectedValue = _settings.ScrollMode;

        ScrollDeadZoneSlider.Value = _settings.ScrollDeadZone * 100;

        ScrollCurveSlider.Value = _settings.ScrollCurve * 100;

        ScrollSmoothingSlider.Value = _settings.ScrollSmoothing * 1000;

        ScrollAxisLockCheck.IsChecked = _settings.ScrollAxisLock;

        ScrollStabilizationCheck.IsChecked = _settings.ScrollStabilization;

        ScrollModifiersCheck.IsChecked = _settings.ScrollModifiers;

        ScrollPrecisionCheck.IsChecked = _settings.ScrollPrecision;

        HapticStrengthSlider.Value = _settings.HapticStrength * 100;

        MagnifierHapticsCheck.IsChecked = _settings.MagnifierHaptics;

        ScrollHapticsCheck.IsChecked = _settings.ScrollHaptics;

        TargetHapticsCheck.IsChecked = _settings.TargetHaptics;

        BoundaryHapticsCheck.IsChecked = _settings.BoundaryHaptics;

        DeadZoneSlider.Value = _settings.DeadZone * 100;

        CurveSlider.Value = _settings.ResponseCurve * 100;

        SmoothingSlider.Value = _settings.Smoothing * 1000;

        InvertCheck.IsChecked = _settings.InvertScroll;

        HapticsCheck.IsChecked = _settings.Haptics;

        NarratorNavigationCheck.IsChecked = _settings.NarratorNavigation;
        ControllerTypeCombo.SelectedIndex = (int)_settings.ControllerType;

        TrayCheck.IsChecked = _settings.MinimizeToTray;

        LightThemeCheck.IsChecked = _settings.LightTheme;

        if (ZoomCombo != null) { SyncLegacyTextSize(); UpdateAccessibleViewport(); }

        PopulatePresentationOptions();

        PopulateVisualAccessOptions();

        TremorCheck.IsChecked = _settings.TremorStabilization;

        MotionLearningCombo.SelectedIndex = (int)_settings.MotionLearning;

        ChordWindowSlider.Value = _settings.ChordWindowMilliseconds;

        RefreshChordList();

        MotionNotice.Text = "";

        StabilizationSlider.Value = _settings.StabilizationStrength * 100;

        HoldSlider.Value = _settings.ClickHoldMilliseconds;

        TargetCheck.IsChecked = _settings.TargetAssist;

        TargetSlider.Value = _settings.TargetAssistStrength * 100;

        HintCheck.IsChecked = _settings.ShowTargetHint;

        RememberCheck.IsChecked = _settings.RememberTargets;

        try

        {

            if (_app.SmokeTest) StartupCheck.IsChecked = false;
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                StartupCheck.IsChecked = key?.GetValue(StartupName) != null;
            }

        }

        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)

        { StartupCheck.IsEnabled = false; Log.Write(ex); }

        _loading = false;

    }



    private void BuildGuide()

    {

        ControlGuide.Children.Clear(); ControlGuide.RowDefinitions.Clear(); ControlGuide.ColumnDefinitions.Clear();

        string[,] rows = {

            { "Left stick", "Move the pointer" }, { "Right stick", "Scroll vertically and horizontally" },

            { "Buttons / triggers", "Customise all 16 controls in Mapping. See the dashboard for active assignments." },

            { "Mouse actions", "Hold to drag (View and Menu click on release)." },

            { "Keyboard actions", "One tap per press. Arrow actions repeat while held." },

            { "Profiles", "Choose a saved profile in the sidebar. Copy, rename, import or export in Profiles." },

            { "View / Menu", "Assigned actions fire on release to protect the pause shortcut." },

            { "View + Menu", "Pause or resume · release both before using again" },

            { "Ctrl + Alt + P", "Pause or resume from the keyboard" }

        };

        ControlGuide.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        ControlGuide.ColumnDefinitions.Add(new ColumnDefinition());

        for (int i = 0; i < rows.GetLength(0); i++)

        {

            ControlGuide.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int j = 0; j < 2; j++)

            {

                var label = new TextBlock
                {
                    Text = rows[i, j],
                    Margin = new Thickness(0, 9, 12, 9),
                    TextWrapping = TextWrapping.Wrap,

                    Foreground = (Brush)FindResource(j == 0 ? "AccentBrush" : "TextBrush")
                };

                label.SetResourceReference(TextBlock.ForegroundProperty, j == 0 ? "AccentBrush" : "TextBrush");

                Grid.SetRow(label, i); Grid.SetColumn(label, j); ControlGuide.Children.Add(label);

            }

        }

    }



    private void RefreshStatus(ControllerStatus? preview = null)

    {

        var status = preview ?? _app.Service?.Status ?? new ControllerStatus(false, true, false, -1, "", default, null, false);

        bool active = status.Connected && status.Enabled && status.Armed && !status.Suspended;

        StatusTitle.Text = status.Suspended ? "Waiting for your desktop" : !status.Enabled ? "Relay is paused"
            : !status.Connected ? "No controller detected" : !status.Armed ? "Release the controls" : "Relay active";

        StatusDetail.Text = status.Suspended ? "Unlock Windows to continue. Relay will wait for you."
            : !status.Enabled ? "Windows control is paused. You can still navigate Relay's menus and choose Resume control. Release all controls after resuming."
            : !status.Connected ? "Connect your Xbox controller by USB, Bluetooth or a wireless adapter. Relay will find it."
            : !status.Armed ? "Let go of all sticks, triggers and buttons for a moment to begin."
            : _app.Service?.KeyboardSelectionActive == true ? _app.Service.FixedSwitchKeyboardLayout
                ? "Keyboard control. B next key. X previous key. A types it. Y hides the keyboard."
                : "Keyboard control. D-pad chooses a key. A types it. B closes the keyboard."
            : _app.Service?.KeyboardNavigationActive == true ? "Menu control. D-pad moves through menus. RB next, LB previous. A selects. B goes back. Move the left stick for mouse control."
            : "Mouse control. The left stick moves the pointer and A clicks. Press the D-pad to use menus in Relay or with Narrator.";

        StatusDot.Fill = new SolidColorBrush(active ? Color.FromRgb(146, 230, 191) : Color.FromRgb(231, 190, 121));

        PauseButton.Content = status.Enabled ? "Pause control" : "Resume control";

        BatteryLabel.Text = status.Connected ? status.Battery.ToUpperInvariant() : "AUTO CONNECT";

        PreviewCaption.Text = status.Connected ? $"Controller {status.Slot + 1} · Live input preview" : "Connect a controller to see your inputs here.";

        ControllerArt.State = status.State;

        ControllerModelText.Text = _app.Service?.ControllerModel ?? "Connect your controller to identify it.";
        ControllerSetupText.Text = _app.Service?.ControllerSetupHint ?? "Choose your controller type if automatic detection is unavailable.";
        var targets = _app.Service?.Targets;

        var prediction = targets?.Prediction;

        _overlay?.Update(prediction, _settings.TargetAssist && _settings.ShowTargetHint && active);

        NavigationFeedback.Text = _app.Service?.FeedbackSummary ?? "Feedback is ready when you are.";

        MemorySummary.Text = $"{targets?.LearnedSelections ?? 0} learned selections · stored locally";

        MotionSummary.Text = _app.Service?.MotionSummary ?? "Motion learning is off. No controller samples collected in preview.";

        TargetStatus.Text = !_settings.TargetAssist ? "Target assist is off." : prediction != null

            ? $"Suggested target: {prediction.Candidate.Kind}. You make the click."

            : "Move toward a nearby button or link to find a target.";

        SteadySummary.Text = (_settings.TremorStabilization ? "Stabilization on" : "Stabilization off")

            + " · " + (_settings.TargetAssist ? "target assist on" : "target assist off");

        Footer.Text = _saveError ?? status.Warning ?? targets?.Warning ?? _app.ProfileStore.Warning ?? _app.Store.Warning ?? (!_hotkeyAvailable

            ? "Ctrl + Alt + P is used by another app. Use the tray or View + Menu to pause."

            : "Settings saved locally · No account needed");
        UpdateCompactFooter();

        RefreshRelay(status, active);

        RefreshPresentation(status);

        ServiceBadge.Text = !status.Enabled ? "Relay paused" : status.Suspended ? "Relay waiting" : active ? "Relay active" : "Relay inactive";
        ProfileHeaderText.Text = _app.Profiles.Active.Name;
        DeviceHeaderText.Text = status.Connected ? _app.Service?.ControllerModel ?? (_settings.ControllerType == ControllerPreference.Adaptive ? "Xbox Adaptive Controller · preview" : "Xbox controller · preview") : "No controller detected";
        if (Pages.SelectedIndex == 0)
        {
            string model = status.Connected ? _app.Service?.ControllerModel ?? (_settings.ControllerType == ControllerPreference.Adaptive ? "Xbox Adaptive Controller" : "Xbox Wireless Controller") : "Control";
            PageHeading.Text = status.Connected && ActualWidth >= 1000 && InterfaceTextScale <= 1.25 ? model : "Control";
            System.Windows.Automation.AutomationProperties.SetName(PageHeading, status.Connected ? "Control. " + model : "Control");
            PageContext.Text = "Your current Relay setup";
        }
        ConnectionHeaderText.Text = !status.Connected ? "Disconnected" : "Connected · " + status.Battery;

        _app.SetTrayStatus(!status.Enabled ? "Paused" : status.Connected ? "Connected" : "Waiting for controller");

    }



    private void SettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!_loading) ReadSettings(); }

    private void OptionChanged(object sender, RoutedEventArgs e) { if (!_loading) ReadSettings(sender != LightThemeCheck && sender != TrayCheck); }

    private void ReadSettings(bool updateInput = true)

    {

        bool lightChanged = _settings.LightTheme != (LightThemeCheck.IsChecked == true);
        if (lightChanged)
        {
            bool light = LightThemeCheck.IsChecked == true;
            var visual = _settings.EffectiveVisualAccess;
            var scheme = visual.Scheme is DisplayScheme.MaximumContrastLight or DisplayScheme.MaximumContrastDark
                ? light ? DisplayScheme.MaximumContrastLight : DisplayScheme.MaximumContrastDark
                : light ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark;
            _settings = _settings with { VisualAccess = visual with { Scheme = scheme } };
        }

        _settings = _settings with

        {

            PointerSpeed = SpeedSlider.Value,
            DpadSpeed = DpadSpeedSlider.Value,
            ScrollSpeed = ScrollSlider.Value,
            DeadZone = DeadZoneSlider.Value / 100,

            ResponseCurve = CurveSlider.Value / 100,
            Smoothing = SmoothingSlider.Value / 1000,

            ControllerType = (ControllerPreference)Math.Max(0, ControllerTypeCombo.SelectedIndex),
            NarratorNavigation = NarratorNavigationCheck.IsChecked == true,
            InvertScroll = InvertCheck.IsChecked == true,
            Haptics = HapticsCheck.IsChecked == true,

            ScrollMode = ScrollModeCombo.SelectedValue is ScrollMode mode ? mode : _settings.ScrollMode,

            ScrollDeadZone = ScrollDeadZoneSlider.Value / 100,
            ScrollCurve = ScrollCurveSlider.Value / 100,

            ScrollSmoothing = ScrollSmoothingSlider.Value / 1000,

            ScrollAxisLock = ScrollAxisLockCheck.IsChecked == true,
            ScrollStabilization = ScrollStabilizationCheck.IsChecked == true,

            ScrollModifiers = ScrollModifiersCheck.IsChecked == true,
            ScrollPrecision = ScrollPrecisionCheck.IsChecked == true,

            HapticStrength = HapticStrengthSlider.Value / 100,
            MagnifierHaptics = MagnifierHapticsCheck.IsChecked == true,

            ScrollHaptics = ScrollHapticsCheck.IsChecked == true,
            TargetHaptics = TargetHapticsCheck.IsChecked == true,

            BoundaryHaptics = BoundaryHapticsCheck.IsChecked == true,

            LightTheme = LightThemeCheck.IsChecked == true,

            MotionLearning = (LearningMode)Math.Max(0, MotionLearningCombo.SelectedIndex),

            ChordWindowMilliseconds = ChordWindowSlider.Value,

            MinimizeToTray = TrayCheck.IsChecked == true,

            TremorStabilization = TremorCheck.IsChecked == true,
            StabilizationStrength = StabilizationSlider.Value / 100,

            ClickHoldMilliseconds = HoldSlider.Value,
            TargetAssist = TargetCheck.IsChecked == true,

            TargetAssistStrength = TargetSlider.Value / 100,
            RememberTargets = RememberCheck.IsChecked == true,

            ShowTargetHint = HintCheck.IsChecked == true

        };

        _app.ApplyVisualAccess(_settings);
        UpdateQuickThemeAction();
        PopulateVisualAccessOptions();

        if (updateInput) _app.Service?.UpdateSettings(_settings, _app.Profiles.ActiveId);

        UpdateLabels(); _save.Stop(); if (!_app.SmokeTest) _save.Start();

    }

    private void UpdateLabels()

    {

        ChordWindowValue.Text = $"{_settings.ChordWindowMilliseconds:0} ms";

        BuildMappingSummary();

        DeadZonePlot.SettingsState = ResponsePlot.SettingsState = _settings;

        OverviewSpeed.Text = $"{_settings.PointerSpeed:0} px/s";

        StabilizationValue.Text = $"{_settings.StabilizationStrength:P0}";

        HoldValue.Text = _settings.ClickHoldMilliseconds == 0 ? "Immediate" : $"{_settings.ClickHoldMilliseconds:0} ms";

        TargetValue.Text = $"{_settings.TargetAssistStrength:P0}";

        StabilizationSlider.IsEnabled = _settings.TremorStabilization;

        TargetSlider.IsEnabled = HintCheck.IsEnabled = RememberCheck.IsEnabled = _settings.TargetAssist;

        SpeedValue.Text = $"{_settings.PointerSpeed:0} px/s";

        DpadSpeedValue.Text = $"{_settings.DpadSpeed:0} pixels per second";

        ScrollValue.Text = $"{_settings.ScrollSpeed:0} notches/s";

        ScrollDeadZoneValue.Text = $"{_settings.ScrollDeadZone:P0}";

        ScrollCurveValue.Text = $"{_settings.ScrollCurve:0.00}×";

        ScrollSmoothingValue.Text = $"{_settings.ScrollSmoothing * 1000:0} ms";

        HapticStrengthValue.Text = $"{_settings.HapticStrength:P0}";

        HapticStrengthSlider.IsEnabled = MagnifierHapticsCheck.IsEnabled = ScrollHapticsCheck.IsEnabled =

            TargetHapticsCheck.IsEnabled = BoundaryHapticsCheck.IsEnabled = _settings.Haptics;

        DeadZoneValue.Text = $"{_settings.DeadZone:P0}";

        CurveValue.Text = $"{_settings.ResponseCurve:0.00}×";

        SmoothingValue.Text = _settings.Smoothing == 0 ? "Off" : $"{_settings.Smoothing * 1000:0} ms";

    }

    private void SaveSettings()

    {

        if (_app.SmokeTest) return;

        try { _app.Profiles = _app.Profiles.Update(_settings); _app.ProfileStore.Save(_app.Profiles); _app.Store.Save(_settings); _app.Service?.SaveMotion(_app.Profiles.Profiles.Select(p => p.Id)); _saveError = null; }

        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)

        { _saveError = "Couldn’t save settings. Current changes will last until you quit."; Log.Write(ex); }

        RefreshStatus();

    }

    private void ApplyPreset(object sender, RoutedEventArgs e)

    {

        var preset = (sender as Button)?.Tag as string;

        _settings = preset switch

        {

            "Precise" => _settings with { PointerSpeed = 650, ScrollSpeed = 5, ResponseCurve = 2, Smoothing = 0.035 },

            "Steady" => _settings with
            {
                PointerSpeed = 850,
                ScrollSpeed = 5,
                ResponseCurve = 1.9,
                Smoothing = .035,

                TremorStabilization = true,
                StabilizationStrength = .7,
                ClickHoldMilliseconds = 100,

                ScrollStabilization = true,
                ScrollAxisLock = true,
                ScrollSmoothing = .035
            },

            "Quick" => _settings with { PointerSpeed = 1800, ScrollSpeed = 12, ResponseCurve = 1.3, Smoothing = 0.015 },

            _ => _settings with { PointerSpeed = 1100, ScrollSpeed = 8, ResponseCurve = 1.65, Smoothing = 0.025 }

        };

        PopulateControls(); ReadSettings();

    }

    private void RestoreDefaults(object sender, RoutedEventArgs e) { _settings = new Settings(); PopulateControls(); LoadBinding(); ReadSettings(); }

    private void StartupChanged(object sender, RoutedEventArgs e)

    {

        if (_app.SmokeTest || _loading) return;

        try

        {

            using var key = Registry.CurrentUser.CreateSubKey(RunKey);

            if (StartupCheck.IsChecked == true) key.SetValue(StartupName, $"\"{Environment.ProcessPath}\" --tray");

            else key.DeleteValue(StartupName, false);

            _saveError = null;

        }

        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)

        { _loading = true; try { StartupCheck.IsChecked = StartupCheck.IsChecked != true; } finally { _loading = false; } _saveError = "Windows did not allow the sign-in preference to change."; Log.Write(ex); }

        RefreshStatus();

    }



    internal void ShowAccessibilityHub() { _app.ShowWindow(); SelectPage(3); }

    private void OpenHub(object sender, RoutedEventArgs e) => ShowAccessibilityHub();

    private void OpenAssist(object sender, RoutedEventArgs e) => SelectPage(2);

    private void MappingChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) ReadSettings(); }

    private void ForgetTargets(object sender, RoutedEventArgs e) { _app.Service?.Targets.Forget(); RefreshStatus(); }

    private void ForgetMotion(object sender, RoutedEventArgs e)

    {

        _app.Service?.ForgetMotion(); RefreshStatus();

        MotionNotice.Text = "Motion learning cleared for this profile. Release all controller controls to resume.";

    }

    private void MotionLearningChanged(object sender, SelectionChangedEventArgs e)

    {

        if (_loading) return;

        ReadSettings();

        MotionNotice.Text = _settings.MotionLearning switch
        {

            LearningMode.Learn => "Learning enabled. Adjustments begin after 5 seconds of qualifying motion and apply between gestures.",

            LearningMode.Frozen => "Learning paused. Existing adjustments remain in use.",

            _ => "Adaptive adjustments are off. Manual settings remain in use."
        };

    }

    private void SearchAccessibility(object sender, TextChangedEventArgs e)

    { if (!_loading && AccessibilityGroups != null) BuildFeatureCards(); }

    private void BuildAccessibility()

    {

        Shortcut[] quick = [Shortcut.ZoomIn, Shortcut.ZoomOut, Shortcut.CloseMagnifier, Shortcut.Narrator,

            Shortcut.OnScreenKeyboard, Shortcut.LiveCaptions, Shortcut.MagnifierRead, Shortcut.ColorFilters, Shortcut.MagnifierView, Shortcut.VoiceTyping];

        foreach (var action in quick)

        {

            var option = AccessibilityCatalog.Actions.First(a => a.Action == action);

            var button = new Button
            {
                Content = new TextBlock { Text = option.Name, TextAlignment = TextAlignment.Center },

                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 12, 10, 12),
                MinHeight = 54,

                IsEnabled = action != Shortcut.LiveCaptions || Environment.OSVersion.Version.Build >= 22621
            };

            System.Windows.Automation.AutomationProperties.SetName(button, option.Name);

            button.Click += (_, _) => { if (!_app.SmokeTest) _app.Service?.InvokeAction(action); };

            QuickTools.Children.Add(button);

        }

        BuildFeatureCards();

    }

    private void BuildFeatureCards()

    {

        AccessibilityGroups.Children.Clear();

        string query = AccessibilitySearch.Text.Trim();

        var features = AccessibilityCatalog.Features.Where(f =>

            (f.Name + " " + f.Description + " " + f.Category).Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var group in features.GroupBy(f => f.Category))

        {

            var groupHeading = PresentationText(group.Key, "SectionFontSize");
            groupHeading.FontWeight = FontWeights.SemiBold; groupHeading.Margin = new Thickness(0, 12, 0, 12);
            AccessibilitySupport.SectionHeading(groupHeading);
            AccessibilityGroups.Children.Add(groupHeading);

            var grid = new UniformGrid { Columns = 2 };

            foreach (var feature in group)

            {

                var content = new StackPanel();

                var featureHeading = PresentationText(feature.Name);
                featureHeading.FontWeight = FontWeights.SemiBold;
                content.Children.Add(featureHeading);

                content.Children.Add(new TextBlock
                {
                    Text = feature.Description,
                    Style = (Style)FindResource("Caption"),

                    Margin = new Thickness(0, 8, 0, 14),
                    MinHeight = 40
                });

                bool available = Environment.OSVersion.Version.Build >= feature.MinimumBuild;

                var button = new Button
                {
                    Content = available ? "Open settings  →" : "Newer Windows required",

                    IsEnabled = available,
                    Padding = new Thickness(12, 8, 12, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                System.Windows.Automation.AutomationProperties.SetName(button, "Open " + feature.Name + " settings");

                button.Click += (_, _) => { if (!_app.SmokeTest) Open(feature.Uri); };

                content.Children.Add(button);

                grid.Children.Add(new Border
                {
                    Style = (Style)FindResource("Card"),
                    Child = content,

                    Padding = new Thickness(18),
                    Margin = new Thickness(0, 0, 10, 10)
                });

            }

            AccessibilityGroups.Children.Add(grid);

        }

        if (AccessibilityGroups.Children.Count == 0)

            AccessibilityGroups.Children.Add(new TextBlock
            {
                Text = "No matching features. Try a different search.",

                Style = (Style)FindResource("Caption"),
                Margin = new Thickness(0, 12, 0, 12)
            });

    }



    private void PreviewHaptic(object sender, RoutedEventArgs e)

    {

        if (!_app.SmokeTest && Enum.TryParse<HapticCue>((sender as Button)?.Tag as string, out var cue))

            _app.Service?.PreviewHaptics(cue);

        RefreshStatus();

    }

    private void ToggleControl(object sender, RoutedEventArgs e) { _app.Service?.Toggle(); RefreshStatus(); }

    private void Quit(object sender, RoutedEventArgs e) { SaveSettings(); _app.ExitApp(); }

    private void OpenAccessibility(object sender, RoutedEventArgs e) => Open("ms-settings:easeofaccess");

    private void OpenDataFolder(object sender, RoutedEventArgs e) { Directory.CreateDirectory(Log.Folder); Open(Log.Folder); }

    private void Open(string target)

    {

        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }

        catch (Win32Exception ex) { _saveError = "Windows could not open that location."; Log.Write(ex); RefreshStatus(); }

    }

    private void OnClosing(object? sender, CancelEventArgs e)

    {

        SaveSettings();

        if (!_app.Exiting && !_app.SmokeTest && _settings.MinimizeToTray)

        {

            e.Cancel = true; Hide();

            if (!_notified) { _app.ShowTrayNotice(); _notified = true; }

        }

        else if (!_app.Exiting && !_app.SmokeTest) Dispatcher.BeginInvoke(new Action(_app.ExitApp));

    }



    internal async System.Threading.Tasks.Task RunSmokeTest(string output)

    {

        Directory.CreateDirectory(output);

        if (Environment.GetCommandLineArgs().Contains("--windows-text-checks-only"))
        {
            RunWindowsTextScaleChecks(output);
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--polish-checks-only"))
        {
            await RunDisclosureChecks(output);
            await RunHoverReadingChecks(output);
            File.WriteAllText(Path.Combine(output, "visual-access-palette-contrast.txt"), VisualAccessTheme.VerifyPaletteContrast());
            File.WriteAllText(Path.Combine(output, "polish-checks.txt"), "PASS: isolated disclosure, hover and palette checks. Full suite not requested.");
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--demo-frames-only"))
        {
            await ExportDemoFrames(output);
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--native-popup-checks-only"))
        {
            await RunNativePopupSwitchChecks(output);
            File.WriteAllText(Path.Combine(output, "native-popup-checks.txt"), "PASS: isolated native dropdown switch checks. The full smoke suite was not requested.");
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--shortcut-checks-only"))
        {
            await RunShortcutCaptureChecks(output);
            File.WriteAllText(Path.Combine(output, "shortcut-checks.txt"), "PASS: isolated shortcut recorder checks. The full smoke suite was not requested.");
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--paused-navigation-checks-only"))
        {
            await RunPausedNavigationChecks(output);
            File.WriteAllText(Path.Combine(output, "paused-navigation-checks.txt"), "PASS: isolated paused navigation checks. The full smoke suite was not requested.");
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--profile-selection-checks-only"))
        {
            await RunProfileSelectionChecks(output);
            File.WriteAllText(Path.Combine(output, "profile-selection-checks.txt"), "PASS: isolated explicit profile-selection checks. The full smoke suite was not requested.");
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--simple-controls-checks-only"))
        {
            await RunSimpleControlChecks(output);
            File.WriteAllText(Path.Combine(output, "simple-controls-checks.txt"), "PASS: isolated single-input stability checks. The full smoke suite was not requested.");
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--visual-checks-only"))
        {
            await RunDisclosureChecks(output);
            await VerifyInputDiagnostics(output);
            RunSurfaceChecks(output);
            RunReflowChecks(output);
            RunWindowsTextScaleChecks(output);
            await RunSettingChoiceChecks(output);
            await RunVisualAccessVerification(output);
            File.WriteAllText(Path.Combine(output, "visual-checks.txt"), "PASS: isolated diagnostics, surface, reflow and display-preference checks. The full smoke suite was not requested.");
            return;
        }

        WindowState = WindowState.Maximized;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (WindowState != WindowState.Maximized) throw new InvalidOperationException("Maximised Relay window failed.");
        WindowState = WindowState.Normal;
        ControllerTypeCombo.SelectedIndex = (int)ControllerPreference.Adaptive;
        if (_settings.ControllerType != ControllerPreference.Adaptive) throw new InvalidOperationException("Adaptive controller choice was not saved.");
        ControllerTypeCombo.SelectedIndex = (int)ControllerPreference.Automatic;
        await RunNarratorNavigationChecks(output);
        await RunPausedNavigationChecks(output);
        await RunHoverReadingChecks(output);
        RunSetupSmokeChecks(output);

        await KeyboardVerification.RenderPreview(output);
        await VerifyAccessories(output);

        await VerifyInputDiagnostics(output);

        if (System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>() != (IntPtr.Size == 8 ? 40 : 28)) throw new InvalidOperationException("Incorrect Windows INPUT layout.");

        ScrollModeCombo.SelectedValue = ScrollMode.Step;

        ScrollDeadZoneSlider.Value = 24; HapticStrengthSlider.Value = 35;

        if (_settings.ScrollMode != ScrollMode.Step || _settings.ScrollDeadZone != .24 || _settings.HapticStrength != .35)

            throw new InvalidOperationException("Scroll and feedback bindings failed.");

        SpeedSlider.Value = 1450;

        if (_settings.PointerSpeed != 1450) throw new InvalidOperationException("Settings binding failed.");

        ApplyPreset(new Button { Tag = "Precise" }, new RoutedEventArgs());

        if (_settings.PointerSpeed != 650) throw new InvalidOperationException("Preset failed.");

        RestoreDefaults(this, new RoutedEventArgs());

        if (_settings != new Settings()) throw new InvalidOperationException("Defaults failed.");



        ApplyPreset(new Button { Tag = "Steady" }, new RoutedEventArgs());

        if (!_settings.TremorStabilization || _settings.ClickHoldMilliseconds != 100 || _settings.TargetAssist)

            throw new InvalidOperationException("Steady preset or prediction opt-in failed.");

        RunProfileSmokeChecks();
        await RunProfileDeleteChecks(output);
        await RunProfileSelectionChecks(output);

        RunChordSmokeChecks();

        RunAccessibilityChecks(output);

        RunSurfaceChecks(output);

        RunReflowChecks(output);

        RunWindowsTextScaleChecks(output);

        await RunSettingChoiceChecks(output);

        await RunSimpleControlChecks(output);

        await RunVisualAccessVerification(output);

        await RunDisclosureChecks(output);

        AccessibilitySearch.Text = "Sticky";

        if (AccessibilityGroups.Children.Count != 2 || ((UniformGrid)AccessibilityGroups.Children[1]).Children.Count != 1)

            throw new InvalidOperationException("Accessibility feature search failed.");

        AccessibilitySearch.Clear();

        RestoreDefaults(this, new RoutedEventArgs());

        Pages.SelectedIndex = 0; Topmost = true; Hide(); Show(); UpdateLayout(); Activate();

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

        // Exercise the actual physical-coordinate UIA detector with our own visible button.

        // Awaiting keeps WPF available to answer UIA requests; no input is injected.

        // Disconnected controllers hide Pause; use an always available header action.
        var probeButton = PageHeaderActions.Children.OfType<Button>().First(button => button.IsVisible && button.IsEnabled);
        probeButton.BringIntoView(); UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        var screenPoint = probeButton.PointToScreen(new Point(probeButton.ActualWidth / 2, probeButton.ActualHeight / 2));

        var windowHandle = new WindowInteropHelper(this).Handle;

        using (var assistant = new WindowsTargetAssistant(null))

        {

            var probe = System.Threading.Tasks.Task.Run(() => assistant.ProbeTargets(

                new PointD(screenPoint.X, screenPoint.Y), new PointD(100, 0), windowHandle));

            var completed = await System.Threading.Tasks.Task.WhenAny(probe, System.Threading.Tasks.Task.Delay(10000));

            if (completed != probe) throw new InvalidOperationException("UI Automation detector timed out.");

            var targets = await probe;

            if (!targets.Any(t => t.Kind == "Button" && t.Contains(new PointD(screenPoint.X, screenPoint.Y))))

                throw new InvalidOperationException($"UI Automation did not find the app's accessible button. Point={screenPoint}; HWND={windowHandle}; foreground={NativeMethods.GetForegroundWindow()}; atPoint={NativeMethods.WindowFromPoint(new NativeMethods.Point { X = (int)screenPoint.X, Y = (int)screenPoint.Y })}; candidates={targets.Count}");

            File.WriteAllText(Path.Combine(output, "target-detection.txt"),

                "PASS: background MTA UI Automation detected a real WPF button in physical screen coordinates. No input injected.");

        }

        var navigationTarget = await System.Threading.Tasks.Task.Run(() => WindowsNavigationObserver.Probe(

            new(0, windowHandle, (int)screenPoint.X, (int)screenPoint.Y, 0, true, false, 0, 0)))

            .WaitAsync(TimeSpan.FromSeconds(10));

        if (navigationTarget.Target == null) throw new InvalidOperationException("Navigation hover detection failed.");

        Pages.SelectedIndex = 5; UpdateLayout();

        var navigationScroll = (ScrollViewer)((TabItem)Pages.Items[5]).Content;

        navigationScroll.ScrollToTop(); UpdateLayout();

        var scrollPoint = navigationScroll.PointToScreen(new Point(60, 100));

        var navigationRequest = new WindowsNavigationObserver.Request(0, windowHandle, (int)scrollPoint.X, (int)scrollPoint.Y, 0, false, true, -1, 0);

        var top = await System.Threading.Tasks.Task.Run(() => WindowsNavigationObserver.Probe(navigationRequest))

            .WaitAsync(TimeSpan.FromSeconds(10));

        navigationScroll.ScrollToBottom(); UpdateLayout();

        var bottomRegion = await System.Threading.Tasks.Task.Run(() => WindowsNavigationObserver.Probe(navigationRequest))

            .WaitAsync(TimeSpan.FromSeconds(10));

        if (top.Region == null || top.Region != bottomRegion.Region || Math.Abs(top.Vertical) > .000001 || Math.Abs(bottomRegion.Vertical - 100) > .000001)

            throw new InvalidOperationException($"ScrollPattern probe failed: {top} -> {bottomRegion}");

        navigationScroll.ScrollToTop(); UpdateLayout();

        File.WriteAllText(Path.Combine(output, "navigation-detection.txt"),

            "PASS: actual WPF target hover and ScrollPattern top/bottom percentages detected on a background MTA thread. No input or vibration injected.");

        for (int tab = 0; tab < Pages.Items.Count; tab++)

        {

            Pages.SelectedIndex = tab; UpdateLayout();

            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);

            bitmap.Render(this);

            using var file = File.Create(Path.Combine(output, $"screen-{tab}.png"));

            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);

        }

        RenderRelayStates(output);

        foreach (int tab in new[] { 2, 3, 5, 6, 8 })

        {

            Pages.SelectedIndex = tab; UpdateLayout();

            var scroll = (ScrollViewer)((TabItem)Pages.Items[tab]).Content;

            scroll.ScrollToBottom(); UpdateLayout();

            var bottom = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);

            bottom.Render(this);

            using var file = File.Create(Path.Combine(output, $"screen-{tab}-bottom.png"));

            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bottom)); encoder.Save(file);

            scroll.ScrollToTop();

        }

        Pages.SelectedIndex = 0; _app.ApplyTheme(true); UpdateLayout();

        var contrast = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);

        contrast.Render(this);

        using (var file = File.Create(Path.Combine(output, "screen-contrast.png")))

        { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(contrast)); encoder.Save(file); }

        _app.ApplyTheme();

        Pages.SelectedIndex = 1; UpdateLayout();

        if (((System.Windows.Controls.TabItem)Pages.Items[1]).Content is ScrollViewer settingsScroll)

        {

            settingsScroll.ScrollToBottom(); UpdateLayout();

            var settingsBitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);

            settingsBitmap.Render(this);

            using var settingsFile = File.Create(Path.Combine(output, "screen-settings-bottom.png"));

            var settingsEncoder = new PngBitmapEncoder(); settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap)); settingsEncoder.Save(settingsFile);

            if (settingsScroll.ScrollableHeight > 0 && settingsScroll.VerticalOffset == 0) throw new InvalidOperationException("Settings scrolling failed.");

        }

        Width = 960; Height = 700; Pages.SelectedIndex = 0; UpdateLayout();

        var small = new RenderTargetBitmap(960, 700, 96, 96, PixelFormats.Pbgra32); small.Render(this);

        using (var file = File.Create(Path.Combine(output, "screen-small.png"))) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(small)); encoder.Save(file); }

        Pages.SelectedIndex = 6; UpdateLayout(); CaptureRelay(output, "mapping-small");

        Pages.SelectedIndex = 8; UpdateLayout(); CaptureRelay(output, "profiles-small");

        _app.ApplyTheme(true); UpdateLayout(); CaptureRelay(output, "profiles-contrast"); _app.ApplyTheme();

        _app.LightTheme = true; _app.ApplyTheme(); UpdateLayout(); CaptureRelay(output, "profiles-light");

        _app.LightTheme = false; _app.ApplyTheme();

        File.WriteAllText(Path.Combine(output, "profiles.txt"), "PASS: mapping apply/reset, profile copy, rename, isolated edits and switching; Mapping/profile renders at minimum size, light and contrast palettes. No user profile files modified.");

        using (var service = new ControllerService(new Settings()))

        {

            service.Toggle(); // Exercise real XInput polling while all desktop output is disabled.

            service.Start();

            System.Threading.Thread.Sleep(1200);

            var status = service.Status;

            if (status.Enabled || status.Warning != null) throw new InvalidOperationException("Paused polling service failed: " + status.Warning);

            File.WriteAllText(Path.Combine(output, "hardware.txt"), status.Connected

                ? $"XInput controller detected in slot {status.Slot + 1}. Physical interaction has not been tested."

                : "No XInput controller detected in any of the four slots. Real-controller acceptance testing is still required.");

            service.Suspend(true, false);

            System.Threading.Thread.Sleep(50);

            if (!service.Status.Suspended) throw new InvalidOperationException("Session suspension failed.");

            service.Suspend(false, false);

        }

        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: startup, native INPUT layout, settings changes, presets, restore defaults, stabilization preset, tool remapping, accessibility search, physical-coordinate UI Automation target detection, eleven page renders, grouped controller navigation, shortcut recording, all seven display schemes and independent preferences, Relay active/paused/light previews, live pointer diagrams, target practice, contrast theme, minimum window size, paused XInput polling, session suspension, service shutdown. No input injected or user settings changed.");

    }

}

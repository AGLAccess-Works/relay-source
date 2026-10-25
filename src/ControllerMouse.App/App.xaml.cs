using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ControllerMouse.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ControllerMouse.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Forms.NotifyIcon? _tray;
    private DispatcherTimer? _instanceTimer;
    private KeyboardWindow? _keyboard;
    private InputDiagnosticsWindow? _diagnosticsWindow;
    internal ControllerService? Service { get; private set; }
    internal void SetVerificationService(ControllerService? service)
    {
        if (!SmokeTest) throw new InvalidOperationException("Service substitution is limited to the isolated verification preview.");
        Service = service;
    }
    internal bool Exiting { get; private set; }
    internal bool SmokeTest { get; private set; }
    internal SettingsStore Store { get; } = new(Path.Combine(Log.Folder, "settings.json"));
    internal ProfileStore ProfileStore { get; } = new(Path.Combine(Log.Folder, "profiles.json"));
    internal ProfileLibrary Profiles { get; set; } = ProfileLibrary.Create(new Settings());

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyTheme();
        SystemParameters.StaticPropertyChanged += OnSystemAppearanceChanged;
        bool keyboardTest = e.Args.Contains("--keyboard-test");
        bool inputTest = e.Args.Contains("--input-test");
        bool pointerTest = e.Args.Contains("--pointer-test") || inputTest;
        SmokeTest = e.Args.Contains("--smoke-test") || keyboardTest || pointerTest;
        InitializeSystemTextScale();
        if (!SmokeTest)
        {
            _mutex = new Mutex(true, @"Local\XboxControllerMouse.App", out bool created);
            if (!created)
            {
                if (!e.Args.Contains("--tray"))
                {
                    try { using var signal = EventWaitHandle.OpenExisting(@"Local\XboxControllerMouse.Show"); signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                }
                _mutex.Dispose(); _mutex = null; Shutdown(); return;
            }
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\XboxControllerMouse.Show");
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write(args.Exception);
            Service?.Dispose(); Service = null;
            MessageBox.Show("Relay needs to close. Details were saved to the local app log.", "Relay", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; ExitApp();
        };
        var settings = SmokeTest ? new Settings() : Store.Load();
        Profiles = SmokeTest ? ProfileLibrary.Create(settings) : ProfileStore.Load(settings);
        settings = Profiles.Active.Settings.WithInterfacePreferences(settings);
        ApplyVisualAccess(settings);
        if (!SmokeTest) Service = new ControllerService(settings, Path.Combine(Log.Folder, "target-memory.json"), Profiles.ActiveId);
        var window = new MainWindow(this, settings);
        MainWindow = window;
        if (!SmokeTest) window.WindowState = WindowState.Maximized;
        if (Service != null) Service.AccessibilityHubRequested += () => Dispatcher.BeginInvoke(new Action(window.ShowAccessibilityHub));
        if (Service != null) Service.KeyboardRequested += () => Dispatcher.BeginInvoke(new Action(ToggleKeyboard));
        if (Service != null) Service.RelayNavigationRequested += command =>
        {
            long generation = Service.CaptureRelayNavigationGeneration(command);
            IntPtr targetWindow = NativeMethods.GetForegroundWindow();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IntPtr foreground = NativeMethods.GetForegroundWindow();
                NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundProcess);
                if (foreground == targetWindow && foregroundProcess == (uint)Environment.ProcessId &&
                    Service?.IsRelayNavigationCurrent(command, generation) == true)
                {
                    if (Windows.OfType<Window>().Any(dialog => new WindowInteropHelper(dialog).Handle == foreground))
                        window.NavigateController(command);
                    else Service.TryNavigateNativeDialog(command, foreground, generation);
                }
            }));
        };
        if (Service != null) Service.KeyboardCommandRequested += command => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_keyboard?.IsVisible == true && Service.KeyboardSelectionActive && Service.Status is { Enabled: true, Armed: true, Suspended: false }) _keyboard.ControllerCommand(command);
        }));
        if (!SmokeTest)
        {
            CreateTray();
            Service!.Start();
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SessionEnding += (_, _) => ExitApp();
            _instanceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _instanceTimer.Tick += (_, _) => { if (_showEvent?.WaitOne(0) == true) ShowWindow(); };
            _instanceTimer.Start();
        }
        // Construct the HWND even for tray startup so the global pause shortcut is available.
        if (e.Args.Contains("--tray") && !SmokeTest && Profiles.SetupCompleted) { window.ShowActivated = false; window.Visibility = Visibility.Hidden; new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle(); }
        else window.Show();
        if (!SmokeTest && e.Args.Contains("--diagnostics")) { Service!.Diagnostics.Enabled = true; OpenDiagnostics(); }
        if (!SmokeTest && !Profiles.SetupCompleted)
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(window.RunSetup));

        if (SmokeTest)
        {
            int index = Array.IndexOf(e.Args, keyboardTest ? "--keyboard-test" : inputTest ? "--input-test" : pointerTest ? "--pointer-test" : "--smoke-test");
            string output = index + 1 < e.Args.Length ? e.Args[index + 1] : Path.Combine(Path.GetTempPath(), "controller-mouse-smoke");
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try
                {
                    if (keyboardTest) await KeyboardVerification.RunInputTest(output);
                    else if (pointerTest)
                    {
                        await window.RunNativeSidebarPointerChecks(output);
                        if (inputTest) await KeyboardVerification.RunInputTest(output);
                    }
                    else await window.RunSmokeTest(output);
                    Shutdown(0);
                }
                catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); Shutdown(1); }
            }));
        }
    }


    private void OnSystemAppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.BeginInvoke(new Action(() => ApplyTheme()));
    }
    private bool _lightTheme = true;
    private VisualAccessPreferences? _visualAccessPreferences;
    internal bool LightTheme
    {
        get => _lightTheme;
        set { _lightTheme = value; _visualAccessPreferences = null; }
    }
    internal VisualAccessPreferences CurrentVisualAccess => _visualAccessPreferences ??
        VisualAccessPreferences.ForScheme(LightTheme ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark);
    internal string[] ThemeAdjustments { get; private set; } = [];
    internal bool ContrastActive { get; private set; }
    internal void ApplyVisualAccess(Settings settings)
    {
        _visualAccessPreferences = settings.EffectiveVisualAccess.Validate();
        _lightTheme = !_visualAccessPreferences.IsDark;
        ApplyTheme();
    }
    internal void ApplyTheme(bool? highContrast = null)
    {
        bool contrast = SystemParameters.HighContrast || highContrast == true;
        ContrastActive = contrast;
        // Explicit light/dark fixture calls keep their legacy meaning; ordinary
        // reapplication (including Windows contrast changes) retains user preferences.
        var preferences = highContrast.HasValue ? VisualAccessPreferences.ForScheme(
            LightTheme ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark) : CurrentVisualAccess;
        VisualAccessTheme.Apply(Resources, preferences, contrast, out string[] adjustments);
        ThemeAdjustments = adjustments;
    }

    private void CreateTray()
    {
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!;
        using (iconStream.Stream)
        using (var sourceIcon = new System.Drawing.Icon(iconStream.Stream))
            _tray = new Forms.NotifyIcon { Text = "Relay", Icon = (System.Drawing.Icon)sourceIcon.Clone(), Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Relay", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("Relay keyboard", null, (_, _) => Dispatcher.Invoke(ToggleKeyboard));
        var toggle = menu.Items.Add("Pause control", null, (_, _) => Dispatcher.Invoke(() => Service?.Toggle()));
        menu.Opening += (_, _) => toggle.Text = Service?.Status.Enabled == true ? "Pause control" : "Resume control";
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
    }

    internal void ShowWindow()
    {
        if (MainWindow is not { } window) return;
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized;
        window.Activate();
    }
    internal void ToggleKeyboard()
    {
        if (_keyboard?.IsVisible == true) { _keyboard.Hide(); return; }
        if (_keyboard == null)
        {
            _keyboard = new KeyboardWindow(keys => Service?.TypeKeys(keys) == true,
                () => Service?.Status is { Enabled: true, Suspended: false });
            _keyboard.IsVisibleChanged += (_, _) => Service?.SetKeyboardOpen(_keyboard?.IsVisible == true);
            _keyboard.Closed += (_, _) => { Service?.SetKeyboardOpen(false); _keyboard = null; };
        }
        _keyboard.ShowNearPointer();
    }
    internal void OpenDiagnostics()
    {
        if (Service == null) return;
        if (_diagnosticsWindow == null)
        {
            _diagnosticsWindow = new InputDiagnosticsWindow(Service.DiagnosticSnapshot, Service.Diagnostics,
                () => { Service.ReleaseAllRelayInputs(); _keyboard?.ClearPendingModifiers(); });
            _diagnosticsWindow.Closed += (_, _) => _diagnosticsWindow = null;
        }
        _diagnosticsWindow.Show(); _diagnosticsWindow.Activate();
    }
    internal void ShowTrayNotice() => _tray?.ShowBalloonTip(3500, "Still ready in your tray", "Controller control is still running. Press Ctrl + Alt + P to pause, or right-click the tray icon to quit.", Forms.ToolTipIcon.Info);
    internal void SetTrayStatus(string status) { if (_tray != null) _tray.Text = ("Relay — " + status)[..Math.Min(63, 8 + status.Length)]; }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect) Service?.Suspend(true, false);
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect) Service?.Suspend(false, false);
    }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) Service?.Suspend(true, true);
        if (e.Mode == PowerModes.Resume) Service?.Suspend(false, true);
    }
    internal void ExitApp() { if (Exiting) return; Exiting = true; Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        Exiting = true;
        _instanceTimer?.Stop();
        _keyboard?.Close();
        _diagnosticsWindow?.Close();
        SystemParameters.StaticPropertyChanged -= OnSystemAppearanceChanged;
        StopSystemTextScale();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Service?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Icon?.Dispose(); _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _showEvent?.Dispose();
        if (_mutex != null) { _mutex.ReleaseMutex(); _mutex.Dispose(); }
        base.OnExit(e);
    }
}

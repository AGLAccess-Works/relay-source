using System;
using System.Runtime.InteropServices;
using ControllerMouse.Core;
using Windows.UI.ViewManagement;

namespace ControllerMouse.App;

public partial class App
{
    private UISettings? _windowsUiSettings;
    internal double WindowsTextScale { get; private set; } = 1;

    private void InitializeSystemTextScale()
    {
        if (SmokeTest) return;
        try
        {
            _windowsUiSettings = new UISettings();
            WindowsTextScale = VisualTextScale.Resolve(1, _windowsUiSettings.TextScaleFactor);
            _windowsUiSettings.TextScaleFactorChanged += WindowsTextScaleChanged;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _windowsUiSettings = null;
            Log.Write(ex);
        }
    }

    private void WindowsTextScaleChanged(UISettings sender, object args) => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (Exiting) return;
        try
        {
            WindowsTextScale = VisualTextScale.Resolve(1, sender.TextScaleFactor);
            (MainWindow as MainWindow)?.RefreshSystemTextScale();
        }
        catch (COMException ex) { Log.Write(ex); }
    }));

    internal void SetVerificationWindowsTextScale(double factor)
    {
        if (!SmokeTest) throw new InvalidOperationException("Text-size substitution requires an isolated preview.");
        WindowsTextScale = VisualTextScale.Resolve(1, factor);
        (MainWindow as MainWindow)?.RefreshSystemTextScale();
    }

    private void StopSystemTextScale()
    {
        if (_windowsUiSettings != null) _windowsUiSettings.TextScaleFactorChanged -= WindowsTextScaleChanged;
        _windowsUiSettings = null;
    }
}

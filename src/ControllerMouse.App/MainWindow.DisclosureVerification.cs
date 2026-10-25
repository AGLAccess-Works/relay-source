using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunDisclosureChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Disclosure verification requires the isolated preview.");
        var settings = _settings;
        int page = Pages.SelectedIndex;
        double width = Width, height = Height;
        var windowState = WindowState;
        bool originalExpanded = _moreVisualOptions!.IsExpanded;
        var report = new StringBuilder();
        Task Drain() => Dispatcher.InvokeAsync(() => UpdateLayout(), DispatcherPriority.ApplicationIdle).Task;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Disclosure accessibility: " + message);
        }
        try
        {
            WindowState = WindowState.Normal;
            foreach (var scheme in new[] { DisplayScheme.RelayDefault, DisplayScheme.DeepDark })
                foreach (double scale in new[] { 1d, 2d })
                {
                    _settings = settings.WithVisualAccessScheme(scheme) with { InterfaceScale = scale };
                    _settings = _settings with { VisualAccess = _settings.EffectiveVisualAccess with { Focus = VisualFocus.VeryStrong } };
                    PopulateControls();
                    _app.ApplyVisualAccess(_settings);
                    Check(_app.CurrentVisualAccess.Scheme == scheme &&
                        (Background as SolidColorBrush)?.Color == (Application.Current.Resources["SurfaceBackgroundBrush"] as SolidColorBrush)?.Color,
                        "the requested appearance was not applied to the window.");
                    Width = scale == 1 ? 1440 : 640; Height = scale == 1 ? 900 : 480;
                    SelectPage(3); _moreVisualOptions.IsExpanded = false;
                    UpdateAccessibleViewport(); await Drain();
                    _moreVisualOptions.ApplyTemplate();
                    var header = _moreVisualOptions.Template.FindName("HeaderSite", _moreVisualOptions) as ToggleButton
                        ?? throw new InvalidOperationException("Disclosure header is missing.");
                    header.BringIntoView(); await Drain();
                    Check(header.ActualWidth >= 44 && header.ActualHeight >= 44, "header target is too small.");
                    var peer = UIElementAutomationPeer.CreatePeerForElement(_moreVisualOptions)!;
                    var expand = peer.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider;
                    Check(expand != null && peer.GetName() == "More display options", "named ExpandCollapse pattern is missing.");
                    Check(header.Focus(), "header cannot receive keyboard focus."); await Drain();
                    foreach (string part in new[] { "DisclosureFocus", "DisclosureFocusInner" })
                    {
                        var ring = header.Template.FindName(part, header) as Border;
                        Check(ring?.BorderBrush is SolidColorBrush { Color.A: 255 } &&
                            new Rect(header.RenderSize).Contains(LayoutBounds(ring, header)), "visible focus outline is missing or clipped.");
                    }
                    foreach (var label in LayoutVisualElements(header).OfType<TextBlock>())
                        CheckCriticalText(label, $"{scheme} disclosure at {scale:P0}");
                    NavigateController(RelayNavigationCommand.Select); await Drain();
                    Check(_moreVisualOptions.IsExpanded && expand!.ExpandCollapseState == ExpandCollapseState.Expanded,
                        "controller Select did not expand the section and its UIA state together.");
                    _schemeChoice!.BringIntoView(); await Drain();
                    Check(_schemeChoice.IsVisible && _schemeChoice.Focus(), "expanded contents cannot be reached.");
                    header.BringIntoView(); await Drain(); Check(header.Focus(), "cannot return to the header.");
                    header.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(header)!, Environment.TickCount, Key.Space)
                    { RoutedEvent = Keyboard.KeyDownEvent });
                    header.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(header)!, Environment.TickCount, Key.Space)
                    { RoutedEvent = Keyboard.KeyUpEvent });
                    await Drain();
                    Check(!_moreVisualOptions.IsExpanded && !_schemeChoice.IsVisible, "Space did not collapse and remove hidden choices.");
                    expand!.Expand(); await Drain(); Check(_schemeChoice.IsVisible, "UIA expansion did not reveal the settings.");
                    expand.Collapse(); await Drain(); Check(!_schemeChoice.IsVisible, "UIA collapse left hidden settings visible.");
                    report.AppendLine($"PASS {scheme}, {scale:P0}: wrapped named 44px disclosure, contained strong focus, controller Select, keyboard Space, UIA ExpandCollapse and reachable expanded settings.");
                    CaptureRelay(output, $"disclosure-{scheme}-{scale * 100:0}");
                }
            File.WriteAllText(Path.Combine(output, "disclosure-accessibility.txt"), report.ToString());
        }
        finally
        {
            _settings = settings; PopulateControls(); _app.ApplyVisualAccess(_settings); Width = width; Height = height; WindowState = windowState;
            _moreVisualOptions.IsExpanded = originalExpanded; SelectPage(page); UpdateAccessibleViewport(); await Drain();
        }
    }
}

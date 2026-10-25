using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async System.Threading.Tasks.Task VerifyInputDiagnostics(string output)
    {
        var events = new InputDiagnostics(); int releases = 0;
        var window = new InputDiagnosticsWindow(() => "Fake XInput state; no device output.", events, () => releases++);
        try
        {
            window.Show(); window.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var tabs = NavigationControls(window).OfType<TabItem>().ToArray();
            if (tabs.Length != 2) throw new InvalidOperationException("Both real diagnostics tabs must be reachable with keyboard/controller navigation.");
            tabs[1].Focus(); NavigateController(RelayNavigationCommand.Select);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (!tabs[1].IsSelected) throw new InvalidOperationException("Controller Select cannot open the Events diagnostics tab.");
            // Native TabItem selection may already transfer focus into its content.
            // If the header retained focus, RB must enter that same selected pane.
            if (System.Windows.Input.Keyboard.FocusedElement is not TextBox)
            {
                NavigateController(RelayNavigationCommand.Next);
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            if (System.Windows.Input.Keyboard.FocusedElement is not TextBox)
                throw new InvalidOperationException("RB cannot enter the selected diagnostics pane: " +
                    (System.Windows.Input.Keyboard.FocusedElement is UIElement element ? UIElementAutomationPeer.CreatePeerForElement(element)?.GetName() : "no focus") + "; order " +
                    string.Join(" | ", NavigationControls(window).Select(control => control.GetType().Name + " " + UIElementAutomationPeer.CreatePeerForElement(control)?.GetName())) + "; texts " +
                    string.Join(" | ", LayoutLogicalElements(window).OfType<TextBox>().Select(text => $"{System.Windows.Automation.AutomationProperties.GetName(text)} visible={text.IsVisible},focusable={text.Focusable},tab={System.Windows.Input.KeyboardNavigation.GetIsTabStop(text)},hit={text.IsHitTestVisible}")));
            static System.Collections.Generic.IEnumerable<DependencyObject> Elements(DependencyObject parent)
            {
                yield return parent;
                foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
                    foreach (var descendant in Elements(child)) yield return descendant;
            }
            int count = 0;
            foreach (var button in Elements(window).OfType<Button>())
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
                if (string.IsNullOrWhiteSpace(peer.GetName()) || peer.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
                    throw new InvalidOperationException("Diagnostics button lacks accessible invocation.");
                if (peer.GetName() == "Release all Relay inputs") invoke.Invoke();
                count++;
            }
            // WPF InvokeProvider dispatches the action asynchronously; direct click exercises the handler too.
            var release = Elements(window).OfType<Button>().First(b => b.Content.ToString() == "Release all Relay inputs");
            release.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (releases < 1 || count != 5) throw new InvalidOperationException("Diagnostics recovery action failed.");
            foreach (var text in Elements(window).OfType<TextBox>())
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(text)!;
                if (string.IsNullOrWhiteSpace(peer.GetName()) || peer.GetPattern(PatternInterface.Value) == null)
                    throw new InvalidOperationException("Diagnostics text is inaccessible.");
            }
            File.WriteAllText(Path.Combine(output, "input-diagnostics.txt"), "PASS: both diagnostics tabs are reachable and controller Select opens Events; RB enters its text pane. Developer window buttons have names and Invoke patterns; state/log text have Value patterns; release/reset callback verified with fake state; no input injected.");
        }
        finally { window.Close(); }
    }
}

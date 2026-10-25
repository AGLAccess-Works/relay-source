using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

internal static class AccessibilitySupport
{
    public static readonly DependencyProperty FocusInnerBrushProperty = DependencyProperty.RegisterAttached(
        "FocusInnerBrush", typeof(System.Windows.Media.Brush), typeof(AccessibilitySupport));
    public static System.Windows.Media.Brush GetFocusInnerBrush(DependencyObject target) =>
        (System.Windows.Media.Brush)target.GetValue(FocusInnerBrushProperty);
    public static void SetFocusInnerBrush(DependencyObject target, System.Windows.Media.Brush value) =>
        target.SetValue(FocusInnerBrushProperty, value);

    private static readonly DependencyProperty CapturesTabInputProperty = DependencyProperty.RegisterAttached(
        "CapturesTabInput", typeof(bool), typeof(AccessibilitySupport),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    internal static void CaptureTabInput(UIElement target) => target.SetValue(CapturesTabInputProperty, true);
    private static readonly ConditionalWeakTable<TextBlock, LiveText> LiveRegions = new();
    internal static void AnnounceChanges(TextBlock text, TimeSpan? delay = null)
    {
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        LiveRegions.GetValue(text, element => new LiveText(element, delay ?? TimeSpan.FromMilliseconds(180)));
    }
    internal static void AnnounceAgain(TextBlock text)
    {
        if (LiveRegions.TryGetValue(text, out var region)) region.Repeat();
    }

    private sealed class LiveText
    {
        private readonly TextBlock _text;
        private readonly DependencyPropertyDescriptor _descriptor;
        private readonly DispatcherTimer _timer;
        private bool _attached;
        private string _lastSpoken;
        internal LiveText(TextBlock text, TimeSpan delay)
        {
            _text = text; _lastSpoken = text.Text;
            _descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            _timer = new DispatcherTimer(DispatcherPriority.Background, text.Dispatcher) { Interval = delay };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                if (!_text.IsVisible || string.IsNullOrWhiteSpace(_text.Text) || _lastSpoken == _text.Text) return;
                _lastSpoken = _text.Text;
                if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                    UIElementAutomationPeer.CreatePeerForElement(_text)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            };
            text.Loaded += (_, _) => Attach();
            text.Unloaded += (_, _) =>
            {
                _timer.Stop();
                if (_attached) _descriptor.RemoveValueChanged(_text, Changed);
                _attached = false;
            };
            if (text.IsLoaded) Attach();
        }
        private void Attach()
        {
            if (_attached) return;
            _attached = true; _lastSpoken = _text.Text;
            _descriptor.AddValueChanged(_text, Changed);
        }
        private void Changed(object? sender, EventArgs e)
        {
            _timer.Stop();
            if (_text.IsVisible) _timer.Start();
        }
        internal void Repeat() { _lastSpoken = ""; Changed(null, EventArgs.Empty); }
    }

    // Controller A sends Enter. WPF check boxes normally only accept Space.
    // Raise the real Click event too so application settings follow the new state.
    internal static void EnableKeyboardAccess(Window window)
    {
        PausedRelayWindowNavigation.Attach(window);
        RelayPointerObservation.Attach(window);
        RelayHoverReader.Attach(window);
        KeyboardNavigation.SetTabNavigation(window, KeyboardNavigationMode.Cycle);
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Tab && !(Keyboard.FocusedElement is DependencyObject target && (bool)target.GetValue(CapturesTabInputProperty)) &&
                Keyboard.Modifiers is (ModifierKeys.None or ModifierKeys.Shift) &&
                NavigateTab(window, Keyboard.Modifiers == ModifierKeys.Shift))
            {
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || e.IsRepeat) return;
            if (Keyboard.FocusedElement is not ToggleButton toggle || !toggle.IsEnabled || !toggle.IsVisible ||
                toggle is not (CheckBox or RadioButton)) return;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, toggle is RadioButton ? true :
                toggle.IsThreeState ? toggle.IsChecked switch { false => true, true => null, _ => false } : toggle.IsChecked != true);
            toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, toggle));
            e.Handled = true;
        };
    }

    internal static bool NavigateTab(Window window, bool backward)
    {
        if (!window.IsVisible || !window.IsEnabled || Application.Current.MainWindow is not MainWindow navigationOwner) return false;
        navigationOwner.NavigateController(backward ? RelayNavigationCommand.Previous : RelayNavigationCommand.Next,
            allowSwitchPopupNavigation: false);
        return true;
    }

    internal static void Heading(TextBlock text, AutomationHeadingLevel level = AutomationHeadingLevel.Level1)
    {
        AutomationProperties.SetHeadingLevel(text, level);
        text.Focusable = true;
        KeyboardNavigation.SetIsTabStop(text, false);
    }
    internal static void SectionHeading(TextBlock text, AutomationHeadingLevel level = AutomationHeadingLevel.Level2)
    {
        AutomationProperties.SetHeadingLevel(text, level);
        KeyboardNavigation.SetIsTabStop(text, false);
    }
    internal static void FocusHeading(TextBlock text)
    {
        text.BringIntoView();
        Keyboard.Focus(text);
    }
}

internal sealed class DiagramAutomationPeer(FrameworkElement owner, string help) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    protected override string GetClassNameCore() => "Relay diagram";
    protected override string GetHelpTextCore() => help;
}

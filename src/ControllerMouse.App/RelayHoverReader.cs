using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControllerMouse.App;

// One dwell owner per Relay window. It announces through the screen reader;
// it never changes focus, selection, input mode or Windows preferences.
internal sealed class RelayHoverReader : IDisposable
{
    private static readonly ConditionalWeakTable<Window, RelayHoverReader> Readers = new();
    private readonly Window _window;
    private readonly Func<bool> _enabled;
    private readonly Func<bool> _eligibleWindow;
    private readonly Action<FrameworkElement, string> _announce;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<UIElement> _roots = [];
    private readonly HashSet<ComboBox> _choices = [];
    private FrameworkElement? _pending, _lastAnnounced;
    private string _lastText = "";
    private Point _anchor;
    private Point _lastPosition, _cancelPosition;
    private NativeMethods.Point _physicalPointer;
    private bool _physicalPointerKnown;
    private bool _disposed, _pointerKnown, _waitingForMovement;
    internal const string ActivityId = "RelayHover";
    internal static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(600);
    internal bool ObservesRoot(UIElement root) => _roots.Contains(root);
    internal bool HasPendingRequest => _pending != null;

    internal static void Attach(Window window, Func<bool>? eligibleWindow = null) => Readers.GetValue(window, owner =>
        new RelayHoverReader(owner, () =>
            (Application.Current?.MainWindow as MainWindow)?.HoverReadingEnabled == true, eligibleWindow: eligibleWindow));
    internal static RelayHoverReader? ForWindow(Window window) =>
        Readers.TryGetValue(window, out var reader) ? reader : null;

    internal RelayHoverReader(Window window, Func<bool> enabled,
        Action<FrameworkElement, string>? announce = null, Func<bool>? eligibleWindow = null)
    {
        _window = window; _enabled = enabled; _eligibleWindow = eligibleWindow ?? (() => window.IsActive);
        _announce = announce ?? ((_, text) => Announce(window, text));
        _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = Dwell };
        _timer.Tick += (_, _) => CompleteDwell();
        AttachRoot(window);
        window.Deactivated += Deactivated;
        window.Closed += Closed;
        if (window.IsLoaded) DiscoverChoices(window);
    }

    private void AttachRoot(UIElement root)
    {
        if (!_roots.Add(root)) return;
        root.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(Moved), true);
        root.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Pressed), true);
        root.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(KeyPressed), true);
        root.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focused), true);
        root.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded), true);
        root.AddHandler(FrameworkElement.UnloadedEvent, new RoutedEventHandler(Unloaded), true);
        root.MouseLeave += Left;
    }
    private void DetachRoot(UIElement root)
    {
        if (!_roots.Remove(root)) return;
        root.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(Moved));
        root.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Pressed));
        root.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(KeyPressed));
        root.RemoveHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focused));
        root.RemoveHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));
        root.RemoveHandler(FrameworkElement.UnloadedEvent, new RoutedEventHandler(Unloaded));
        root.MouseLeave -= Left;
    }
    private void Loaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource == sender && sender is DependencyObject root) DiscoverChoices(root);
    }
    private void DiscoverChoices(DependencyObject root)
    {
        if (root is ComboBox choice) TrackChoice(choice);
        if (root is not Visual) return;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            DiscoverChoices(VisualTreeHelper.GetChild(root, i));
    }
    private void TrackChoice(ComboBox choice)
    {
        if (!_choices.Add(choice)) return;
        RelayPointerObservation.AttachChoice(choice);
        choice.DropDownOpened += ChoiceOpened;
        choice.DropDownClosed += ChoiceClosed;
    }
    private void ChoiceOpened(object? sender, EventArgs e)
    {
        Cancel();
        if (sender is ComboBox choice && choice.Template.FindName("PART_Popup", choice) is Popup { Child: { } child })
            AttachRoot(child);
    }
    private void ChoiceClosed(object? sender, EventArgs e)
    {
        Cancel();
        if (sender is ComboBox choice && choice.Template.FindName("PART_Popup", choice) is Popup { Child: { } child })
            DetachRoot(child);
    }
    private void Unloaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource == _pending || e.OriginalSource == sender) Cancel();
    }
    private void Moved(object sender, MouseEventArgs e)
    {
        // Layout can synthesize mouse movement under an unmoved pointer. After
        // navigation/rebuilding, only a real movement may resume hover reading.
        if (NativeMethods.GetPhysicalCursorPos(out var point))
        {
            bool moved = !_physicalPointerKnown || point.X != _physicalPointer.X || point.Y != _physicalPointer.Y;
            _physicalPointer = point; _physicalPointerKnown = true;
            if (_waitingForMovement && !moved) return;
        }
        Observe(e.OriginalSource as DependencyObject, e.GetPosition(_window));
    }
    private void Pressed(object sender, MouseButtonEventArgs e)
    {
        if (ResolveTarget(e.OriginalSource as DependencyObject) is ComboBox choice) TrackChoice(choice);
        Cancel();
    }
    private void KeyPressed(object sender, KeyEventArgs e) => Cancel();
    private void Focused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ResolveTarget(e.NewFocus as DependencyObject) is ComboBox choice) TrackChoice(choice);
        Cancel();
    }
    private void Left(object sender, MouseEventArgs e)
    {
        Cancel(); _lastAnnounced = null; _lastText = ""; _pointerKnown = _waitingForMovement = false;
    }
    private void Deactivated(object? sender, EventArgs e) => Cancel();
    private void Closed(object? sender, EventArgs e) => Dispose();

    internal void Observe(DependencyObject? source, Point position)
    {
        if (_disposed || !_enabled() || !_eligibleWindow() || !_window.IsVisible) { Cancel(); return; }
        if (_waitingForMovement && (position - _cancelPosition).Length <= 4) return;
        _waitingForMovement = false; _pointerKnown = true; _lastPosition = position;
        var target = ResolveTarget(source);
        if (target is ComboBox choice) TrackChoice(choice);
        if (target == null || !target.IsVisible || !OpenChoiceTarget(target))
        {
            _timer.Stop(); _pending = null; _lastAnnounced = null; _lastText = ""; return;
        }
        if (target.IsKeyboardFocusWithin) { Cancel(); return; }
        string text = ReadText(target);
        if (string.IsNullOrWhiteSpace(text) || target == _lastAnnounced && text == _lastText)
        { _timer.Stop(); _pending = null; return; }
        if (_pending != target || (position - _anchor).Length > 4)
        {
            _pending = target; _anchor = position;
            _timer.Stop(); _timer.Start();
        }
    }
    internal void CompleteDwell()
    {
        _timer.Stop();
        var target = _pending; _pending = null;
        if (_disposed || !_enabled() || !_eligibleWindow() || !_window.IsVisible ||
            target == null || !target.IsVisible || !target.IsLoaded || target.IsKeyboardFocusWithin || !OpenChoiceTarget(target)) return;
        string text = ReadText(target);
        if (string.IsNullOrWhiteSpace(text) || target == _lastAnnounced && text == _lastText) return;
        _lastAnnounced = target; _lastText = text;
        _announce(target, text);
    }
    internal void Cancel()
    {
        _timer.Stop(); _pending = null;
        _cancelPosition = _lastPosition; _waitingForMovement = _pointerKnown;
        _physicalPointerKnown = NativeMethods.GetPhysicalCursorPos(out _physicalPointer);
    }

    private static bool OpenChoiceTarget(FrameworkElement target) => target is not ComboBoxItem item ||
        ItemsControl.ItemsControlFromItemContainer(item) is not ComboBox choice || choice.IsDropDownOpen;

    internal static FrameworkElement? ResolveTarget(DependencyObject? source)
    {
        TextBlock? text = null;
        for (var element = source; element != null; element = Parent(element))
        {
            if (element is PasswordBox password) return password;
            if (element is ButtonBase or ComboBoxItem or ListBoxItem or ComboBox or ListBox or Slider or TextBox or MenuItem)
                return (FrameworkElement)element;
            if (element is TextBlock label && element is not DecorativeGlyph && !string.IsNullOrWhiteSpace(label.Text))
                text ??= label;
            if (element is Window) break;
        }
        return text;
    }
    private static DependencyObject? Parent(DependencyObject element) =>
        (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);

    internal static string ReadText(FrameworkElement target)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(target);
        string name = peer?.GetName() ?? "";
        if (string.IsNullOrWhiteSpace(name) && target is TextBlock text) name = text.Text;
        if (string.IsNullOrWhiteSpace(name)) return "";
        if (target is PasswordBox || peer?.IsPassword() == true) return name + ". Password field.";
        var parts = new List<string> { name };
        if (!target.IsEnabled) parts.Add("Unavailable");
        string status = peer?.GetItemStatus() ?? "";
        if (!string.IsNullOrWhiteSpace(status)) parts.Add(status);
        void AddState(string state)
        {
            // Keyboard keys already expose their modifier state in ItemStatus.
            // Include it once rather than saying "On. On" or "Off. Off".
            if (!Array.Exists(status.Split('.'), segment => segment.Trim().Equals(state, StringComparison.OrdinalIgnoreCase)))
                parts.Add(state);
        }
        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
            AddState(toggle.ToggleState switch { ToggleState.On => "On", ToggleState.Off => "Off", _ => "Mixed" });
        else if (peer?.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selected && selected.IsSelected)
            AddState("Selected");
        else if (target is ComboBox combo && combo.SelectedItem is { } selectedItem)
        {
            string value = !string.IsNullOrWhiteSpace(combo.Text) ? combo.Text : selectedItem is FrameworkElement selectedElement
                ? UIElementAutomationPeer.CreatePeerForElement(selectedElement)?.GetName() ?? "" : selectedItem.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(value);
        }
        else if (string.IsNullOrWhiteSpace(status) && peer?.GetPattern(PatternInterface.RangeValue) is IRangeValueProvider range)
            parts.Add(range.Value.ToString("0.##", CultureInfo.CurrentCulture));
        else if (peer?.GetPattern(PatternInterface.Value) is IValueProvider value && !string.IsNullOrWhiteSpace(value.Value))
            parts.Add(value.Value);
        return string.Join(". ", parts);
    }
    private static void Announce(Window window, string text)
    {
        // One stable window peer lets MostRecent coalesce this hover activity.
        // The user's screen reader owns speech; no second voice runs alongside it.
        try
        {
            UIElementAutomationPeer.CreatePeerForElement(window)?.RaiseNotificationEvent(
                AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, text, ActivityId);
        }
        catch (ElementNotAvailableException) { } // A disappearing screen-reader provider is optional feedback.
        catch (System.Runtime.InteropServices.COMException) { }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Cancel();
        foreach (var root in new List<UIElement>(_roots)) DetachRoot(root);
        foreach (var choice in _choices)
        {
            choice.DropDownOpened -= ChoiceOpened;
            choice.DropDownClosed -= ChoiceClosed;
        }
        _choices.Clear();
        _window.Deactivated -= Deactivated; _window.Closed -= Closed;
    }
}

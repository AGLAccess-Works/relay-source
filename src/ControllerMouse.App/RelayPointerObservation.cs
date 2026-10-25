using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ControllerMouse.App;

// Reuse the same native pointer observer for owned dialogs and dropdowns.
// Hooks observe messages; they never capture, consume or redirect mouse input.
internal static class RelayPointerObservation
{
    private static readonly ConditionalWeakTable<Window, object> Windows = new();
    private static readonly ConditionalWeakTable<ComboBox, ChoiceObservation> Choices = new();
    internal static void AttachChoice(ComboBox choice) => Choices.GetValue(choice, item => new ChoiceObservation(item));
    internal static bool ObservesChoicePopup(ComboBox choice) =>
        Choices.TryGetValue(choice, out var observer) && observer.Attached;
    internal static void Attach(Window window)
    {
        if (Windows.TryGetValue(window, out _)) return;
        Windows.Add(window, new object());
        HwndSource? source = null;
        void AttachSource()
        {
            if (window is MainWindow || source != null) return;
            source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook(Observe);
        }
        window.SourceInitialized += (_, _) => AttachSource();
        window.Closed += (_, _) => { source?.RemoveHook(Observe); source = null; };
        if (PresentationSource.FromVisual(window) != null) AttachSource();
        window.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is ComboBox choice) AttachChoice(choice);
        }));
    }
    private sealed class ChoiceObservation
    {
        private readonly ComboBox _choice;
        private Popup? _popup;
        private HwndSource? _source;
        internal bool Attached => _source != null;
        internal ChoiceObservation(ComboBox choice)
        {
            _choice = choice;
            choice.DropDownOpened += (_, _) =>
            {
                Detach();
                if (choice.Template.FindName("PART_Popup", choice) is Popup popup)
                {
                    _popup = popup;
                    // ComboBox can announce its open state before the popup's
                    // native presentation source exists. Popup.Opened supplies
                    // the actual source, including on subsequent opens.
                    popup.Opened += PopupOpened;
                    popup.Closed += PopupClosed;
                    AttachSource();
                }
            };
            choice.DropDownClosed += (_, _) => Detach();
            choice.Unloaded += (_, _) => Detach();
        }
        private void PopupOpened(object? sender, EventArgs e) => AttachSource();
        private void PopupClosed(object? sender, EventArgs e) => Detach();
        private void AttachSource()
        {
            if (!_choice.IsDropDownOpen || _popup?.Child is not { } child) return;
            var source = PresentationSource.FromVisual(child) as HwndSource;
            if (source == _source) return;
            _source?.RemoveHook(Observe);
            _source = source;
            _source?.AddHook(Observe);
        }
        private void Detach()
        {
            _source?.RemoveHook(Observe); _source = null;
            if (_popup != null)
            {
                _popup.Opened -= PopupOpened;
                _popup.Closed -= PopupClosed;
                _popup = null;
            }
        }
    }
    private static IntPtr Observe(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Application.Current?.MainWindow is MainWindow main) main.ObservePointerMessage(message);
        return IntPtr.Zero;
    }
}

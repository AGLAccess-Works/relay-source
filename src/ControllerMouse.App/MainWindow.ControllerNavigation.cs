using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    internal event Action? PointerInteractionObserved;
    private NativeMethods.Point _lastNavigationPointer;
    private bool _navigationPointerKnown;

    internal void ObservePointerMessage(int message)
    {
        if (message is not (0x0200 or 0x0201 or 0x0204 or 0x0207)) return;
        bool moved = NativeMethods.GetPhysicalCursorPos(out var point) &&
            (!_navigationPointerKnown || point.X != _lastNavigationPointer.X || point.Y != _lastNavigationPointer.Y);
        _lastNavigationPointer = point;
        _navigationPointerKnown = true;
        // Tagged stick output may move the pointer while a D-pad press navigates.
        // Only a separate mouse interaction returns the controller to pointer clicks.
        if (unchecked((ulong)NativeMethods.GetMessageExtraInfo().ToInt64()) == WindowsInputSink.RelayMouseMarker) return;
        if (message == 0x0200 && !moved) return;
        _app.Service?.NotifyPointerInteraction(clicked: message != 0x0200);
        PointerInteractionObserved?.Invoke();
    }

    // This is the sole owner of controller menu navigation while Relay has focus.
    // No native key is injected by this handler.
    internal void NavigateController(RelayNavigationCommand command, bool allowSwitchPopupNavigation = true)
    {
        var focused = Keyboard.FocusedElement as UIElement;
        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive && window.IsEnabled)
            ?? (focused == null ? null : NavigationWindow(focused))
            ?? (IsVisible && IsEnabled ? this : null);
        if (activeWindow == null || !activeWindow.IsVisible || !activeWindow.IsEnabled) return;
        if (activeWindow == this) RefreshNavigationGroups();
        RelayHoverReader.ForWindow(activeWindow)?.Cancel();
        if (focused == null || !AvailableNavigationElement(focused) ||
            NavigationWindow(focused) is { } focusWindow && focusWindow != activeWindow)
        {
            // A closed dialog or rebuilt page can leave no usable keyboard focus.
            // Recover once; the same press must not then advance a second target.
            if (activeWindow == this) FocusNavigationTarget(CurrentNavigation());
            else FocusNavigationTarget(NavigationControls(activeWindow).FirstOrDefault());
            return;
        }
        var sidebar = NavigationItems().ToArray();
        if (activeWindow == this && focused is RadioButton radio && sidebar.Contains(radio))
        {
            int current = Array.IndexOf(sidebar, radio);
            if (command is RelayNavigationCommand.Up or RelayNavigationCommand.Down)
            {
                int next = Math.Clamp(current + (command == RelayNavigationCommand.Down ? 1 : -1), 0, sidebar.Length - 1);
                var target = sidebar[next];
                if (FocusNavigationTarget(target)) target.IsChecked = true;
                return;
            }
            if (command is RelayNavigationCommand.Left or RelayNavigationCommand.Back) return;
            if (command == RelayNavigationCommand.Select) { radio.IsChecked = true; return; }
            if (command is RelayNavigationCommand.Right or RelayNavigationCommand.Next) { AccessibilitySupport.FocusHeading(PageHeading); return; }
        }
        var pageRoot = activeWindow == this ? Pages.SelectedContent as DependencyObject : activeWindow;
        var pageControls = pageRoot == null ? [] : NavigationControls(pageRoot).ToArray();
        if (focused is TextBlock heading && AutomationProperties.GetHeadingLevel(heading) != AutomationHeadingLevel.None)
        {
            // A heading is deliberately focusable for its announcement, but is
            // not a tab stop. WPF spatial traversal cannot reliably enter its page.
            if (activeWindow == this && command is (RelayNavigationCommand.Up or RelayNavigationCommand.Left or
                RelayNavigationCommand.Previous or RelayNavigationCommand.Back))
                FocusNavigationTarget(CurrentNavigation());
            else if (command is RelayNavigationCommand.Down or RelayNavigationCommand.Right or RelayNavigationCommand.Next or RelayNavigationCommand.Select)
                FocusNavigationTarget(pageControls.FirstOrDefault());
            else if (command is RelayNavigationCommand.Up or RelayNavigationCommand.Left or RelayNavigationCommand.Previous)
                FocusNavigationTarget(pageControls.LastOrDefault());
            else if (command == RelayNavigationCommand.Back && !DispatchControlKey(focused, Key.Escape)) activeWindow.Close();
            return;
        }
        if (command is RelayNavigationCommand.Next or RelayNavigationCommand.Previous)
        {
            if (allowSwitchPopupNavigation && HasFixedSwitchPopupControls() &&
                FindControl<ComboBox>(focused) is { IsDropDownOpen: true } switchChoice)
            {
                // A four-switch layout has no separate arrow inputs. Keep B/X
                // inside the open choice and use its native highlight semantics;
                // only Select commits, Back cancels, and endpoints stay owned.
                DispatchControlKey(switchChoice, command == RelayNavigationCommand.Next ? Key.Down : Key.Up);
                RevealFocusedControl(); return;
            }
            var targets = activeWindow == this
                ? MainNavigationOrder(pageControls)
                : pageControls;
            MoveInNavigationOrder(focused, targets, command == RelayNavigationCommand.Next, activeWindow);
            return;
        }
        if (command == RelayNavigationCommand.Back)
        {
            if (FindControl<ComboBox>(focused) is { IsDropDownOpen: true } combo) { combo.IsDropDownOpen = false; return; }
            // Read-only recorders accept literal physical Escape. A controller's
            // Back must leave that dialog, rather than record a synthetic Escape.
            if (focused is TextBox { IsReadOnly: true })
            {
                if (activeWindow != this) activeWindow.Close();
                else FocusNavigationTarget(CurrentNavigation());
                return;
            }
            if (DispatchControlKey(focused, Key.Escape)) return;
            if (activeWindow != this) { activeWindow.Close(); return; }
            FocusNavigationTarget(CurrentNavigation()); return;
        }
        if (command == RelayNavigationCommand.Select)
        {
            // A read-only field has no activation action. Dispatching Enter here
            // would make the shortcut recorder mistake Select for a recorded key.
            if (focused is TextBox { IsReadOnly: true }) return;
            var peer = UIElementAutomationPeer.CreatePeerForElement(focused);
            if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle) { toggle.Toggle(); return; }
            if (peer?.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selection)
            {
                selection.Select();
                if (FindControl<ComboBox>(focused) is { } owner) { owner.IsDropDownOpen = false; owner.Focus(); }
                return;
            }
            if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) { invoke.Invoke(); return; }
            if (peer?.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand)
            {
                if (expand.ExpandCollapseState == ExpandCollapseState.Collapsed) expand.Expand(); else expand.Collapse();
                return;
            }
            DispatchControlKey(focused, Key.Enter); return;
        }
        Key key = command switch
        {
            RelayNavigationCommand.Up => Key.Up,
            RelayNavigationCommand.Down => Key.Down,
            RelayNavigationCommand.Left => Key.Left,
            RelayNavigationCommand.Right => Key.Right,
            _ => Key.None
        };
        if (key == Key.None) return;
        if (activeWindow == this && FindNavigationGroup(focused) is { } inputGroup)
        {
            inputGroup.Move(focused, command is RelayNavigationCommand.Down or RelayNavigationCommand.Right);
            return;
        }
        // Value and text controls retain their own WPF arrow-key semantics.
        if (FindControl<Slider>(focused) != null || FindControl<ComboBox>(focused) != null ||
            FindControl<ListBox>(focused) != null || FindControl<TextBox>(focused) != null)
        {
            // Popup items live in another presentation source. Route arrows to
            // their ComboBox owner, whose WPF handler maintains the highlighted
            // item without committing its value until Select. The popup owns this
            // direction even at an endpoint; it must not fall through to the page.
            if (FindControl<ComboBox>(focused) is { IsDropDownOpen: true } openChoice)
            {
                DispatchControlKey(openChoice, key); RevealFocusedControl(); return;
            }
            if (DispatchControlKey(focused, key)) { RevealFocusedControl(); return; }
        }
        if (command is (RelayNavigationCommand.Left or RelayNavigationCommand.Right) && pageRoot is Visual coordinateRoot)
        {
            var target = HorizontalNavigationTarget(focused, pageControls, coordinateRoot, command == RelayNavigationCommand.Right);
            if (target != null) { FocusNavigationTarget(target); return; }
        }
        MoveInNavigationOrder(focused, pageControls,
            command is RelayNavigationCommand.Down or RelayNavigationCommand.Right, activeWindow);
    }

    private bool HasFixedSwitchPopupControls()
    {
        if (_app.Service is { } service) return service.FixedSwitchKeyboardLayout;
        bool Matches(ControllerMouse.Core.Control control, int key, int modifiers = 0) =>
            ControlMapping.Resolve(_settings, control) is { Kind: BindingKind.Keyboard } binding &&
            binding.Key == key && binding.Modifiers == modifiers;
        return Matches(ControllerMouse.Core.Control.A, 13) && Matches(ControllerMouse.Core.Control.B, 9) &&
            Matches(ControllerMouse.Core.Control.X, 9, 4) && Matches(ControllerMouse.Core.Control.Y, 27);
    }

    // Enumerate actual actionable controls in their stable content/read order.
    // Template thumbs, hidden TabItems, scroll bars and decorative shell elements
    // are not destinations. Off-screen page controls remain reachable by scrolling.
    private static IEnumerable<System.Windows.Controls.Control> NavigationControls(DependencyObject root)
    {
        if (root is UIElement element && !AvailableNavigationElement(element)) yield break;
        // The native TabControl visual tree puts its selected content before its
        // header panel. Use the visible reading order: tabs, then their active pane.
        if (root is TabControl tabs)
        {
            if (tabs.Name != "Pages")
                foreach (var tabHeader in tabs.Items.OfType<TabItem>())
                    foreach (var target in NavigationControls(tabHeader)) yield return target;
            if (tabs.SelectedContent is DependencyObject content)
                foreach (var target in NavigationControls(content)) yield return target;
            yield break;
        }
        if (root is System.Windows.Controls.Control control && control.Focusable &&
            (KeyboardNavigation.GetIsTabStop(control) || control is ListBox) &&
            control is not (Window or TabControl or ScrollViewer or ScrollBar or Thumb or RepeatButton) &&
            !(control is TabItem tab && ItemsControl.ItemsControlFromItemContainer(tab) is TabControl { Name: "Pages" }))
        {
            yield return control;
            if (control is ButtonBase or ComboBox or ListBox or TextBoxBase or Slider) yield break;
        }
        if (root is not Visual) yield break;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in NavigationControls(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static bool AvailableNavigationElement(UIElement element)
    {
        if (!element.IsVisible || !element.IsEnabled || PresentationSource.FromVisual(element) == null) return false;
        // The Popup host belongs to the owner's logical tree, but its visible
        // items have their own visual presentation tree. Do not cross that island
        // and mistake its non-rendering logical host for a hidden focus target.
        for (DependencyObject? current = element; current is Visual; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ancestor && (!ancestor.IsVisible || !ancestor.IsEnabled ||
                !ancestor.IsHitTestVisible || ancestor.Opacity < .01)) return false;
        return true;
    }

    private static DependencyObject? NavigationParent(DependencyObject element) =>
        (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);

    private static Window? NavigationWindow(UIElement element) => FindControl<ComboBox>(element) is { } combo
        ? Window.GetWindow(combo) : Window.GetWindow(element);

    private static bool FocusNavigationTarget(UIElement? target)
    {
        if (target == null || !AvailableNavigationElement(target)) return false;
        if (target is FrameworkElement element) element.BringIntoView();
        return target.IsKeyboardFocused || target.Focus();
    }

    private void MoveInNavigationOrder(UIElement focused, System.Windows.Controls.Control[] targets, bool forward, Window activeWindow)
    {
        var group = activeWindow == this ? FindNavigationGroup(focused) : null;
        int index = Array.FindIndex(targets, target => NavigationContains(target, focused) || group?.Contains(target) == true);
        if (index < 0) { FocusNavigationTarget(forward ? targets.FirstOrDefault() : targets.LastOrDefault()); return; }
        int next = index + (forward ? 1 : -1);
        if (next < 0)
        {
            if (activeWindow == this) AccessibilitySupport.FocusHeading(PageHeading);
            else FocusNavigationTarget(targets.LastOrDefault());
        }
        else if (next >= targets.Length)
        {
            if (activeWindow != this) FocusNavigationTarget(targets.FirstOrDefault());
            // D-pad endpoints clamp; RB's explicit list contains header actions
            // and the current sidebar item, so those actions remain reachable.
        }
        else FocusNavigationTarget(targets[next]);
    }

    private static bool NavigationContains(DependencyObject target, DependencyObject element)
    {
        if (ReferenceEquals(target, element)) return true;
        if (element is ComboBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), target)) return true;
        for (var parent = NavigationParent(element); parent != null; parent = NavigationParent(parent))
            if (ReferenceEquals(parent, target)) return true;
        return false;
    }

    private static FrameworkElement? NavigationPane(DependencyObject element, DependencyObject root)
    {
        for (var parent = NavigationParent(element); parent != null && !ReferenceEquals(parent, root); parent = NavigationParent(parent))
            if (parent is FrameworkElement pane && (pane.Name.EndsWith("Pane", StringComparison.Ordinal) ||
                pane.Name is "ActionCategoriesHost" or "ControlTilesHost" or "HardwareInputsHost")) return pane;
        return null;
    }

    private static System.Windows.Controls.Control? HorizontalNavigationTarget(UIElement focused,
        System.Windows.Controls.Control[] targets, Visual root, bool right)
    {
        var current = targets.FirstOrDefault(target => NavigationContains(target, focused));
        if (current == null) return null;
        var pane = NavigationPane(current, root);
        var bounds = current.TransformToAncestor(root).TransformBounds(new Rect(current.RenderSize));
        var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        var candidates = targets.Where(target => target != current).Select(target =>
        {
            var rectangle = target.TransformToAncestor(root).TransformBounds(new Rect(target.RenderSize));
            return (Target: target, Pane: NavigationPane(target, root), Center: new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2));
        }).ToArray();
        if (pane != null)
        {
            // Content order remains stable when a responsive layout stacks one
            // pane above another. Geometry alone skipped Mapping's drawing when
            // its categories wrapped above it but the inspector stayed beside it.
            var panes = targets.Select(target => NavigationPane(target, root)).Where(target => target != null).Distinct().ToArray();
            int index = Array.IndexOf(panes, pane);
            int adjacent = index + (right ? 1 : -1);
            if (adjacent >= 0 && adjacent < panes.Length)
            {
                return candidates.Where(candidate => candidate.Pane == panes[adjacent])
                    .OrderBy(candidate => Math.Abs(candidate.Center.Y - center.Y) * 2 + Math.Abs(candidate.Center.X - center.X))
                    .Select(candidate => candidate.Target).FirstOrDefault();
            }
            candidates = candidates.Where(candidate => candidate.Pane == pane).ToArray();
        }
        return candidates.Where(candidate => right ? candidate.Center.X > center.X + 2 : candidate.Center.X < center.X - 2)
            .OrderBy(candidate => Math.Abs(candidate.Center.Y - center.Y) * 2 + Math.Abs(candidate.Center.X - center.X))
            .Select(candidate => candidate.Target).FirstOrDefault();
    }
    private static T? FindControl<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T control) return control;
            if (element is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is T owner) return owner;
            element = (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);
        }
        return null;
    }
    private static void RevealFocusedControl()
    {
        if (Keyboard.FocusedElement is FrameworkElement element) element.BringIntoView();
    }
    private static bool DispatchControlKey(UIElement control, Key key)
    {
        if (PresentationSource.FromVisual(control) is not { } source) return false;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        control.RaiseEvent(args);
        if (!args.Handled) { args.RoutedEvent = Keyboard.KeyDownEvent; control.RaiseEvent(args); }
        return args.Handled;
    }
}

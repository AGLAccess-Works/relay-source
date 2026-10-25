using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ControllerMouse.Core;
using UiControl = System.Windows.Controls.Control;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private readonly Dictionary<FrameworkElement, RovingNavigationGroup> _navigationGroups = new();
    private bool _navigationGroupsInitialized;

    internal void InitializeNavigationGroups()
    {
        if (_navigationGroupsInitialized) return;
        _navigationGroupsInitialized = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is not UIElement focused) return;
            var command = e.Key switch
            {
                Key.Up => RelayNavigationCommand.Up,
                Key.Down => RelayNavigationCommand.Down,
                Key.Left => RelayNavigationCommand.Left,
                Key.Right => RelayNavigationCommand.Right,
                _ => (RelayNavigationCommand)(-1)
            };
            if ((int)command < 0 || FindNavigationGroup(focused) is not { } group) return;
            RelayHoverReader.ForWindow(this)?.Cancel();
            group.Move(focused, command is RelayNavigationCommand.Down or RelayNavigationCommand.Right);
            e.Handled = true;
        };
        RefreshNavigationGroups();
    }

    internal void RefreshNavigationGroups()
    {
        if (!_navigationGroupsInitialized) { InitializeNavigationGroups(); return; }
        foreach (var (surface, name) in new[] { (LiveSurface, "Controller inputs"), (MappingSurface, "Choose an input"), (HardwareSurface, "Hardware inputs") })
            UpdateGroup(surface, name, surface.Parts.Values);
        UpdateGroup(ControlTilesHost, "Current assignments", _controlTiles.Values.Select(tile => tile.Button));
        UpdateGroup(HardwareInputsHost, "All hardware assignments", _hardwareTiles.Values.Select(tile => tile.Button));
    }

    private void UpdateGroup(FrameworkElement host, string name, IEnumerable<UiControl> members)
    {
        if (!_navigationGroups.TryGetValue(host, out var group))
        {
            group = new(host, name);
            _navigationGroups.Add(host, group);
        }
        group.Refresh(members);
    }

    private RovingNavigationGroup? FindNavigationGroup(DependencyObject target) =>
        _navigationGroups.Values.FirstOrDefault(group => group.Contains(target));

    private bool IsGroupedNavigationMember(DependencyObject target) => FindNavigationGroup(target) != null;

    private UiControl[] MainNavigationOrder(UiControl[] pageControls) => pageControls
        .Concat(NavigationControls(PageHeaderActions)).Append(ProfileCombo).Append(CurrentNavigation()).Distinct().ToArray();

    private sealed class RovingNavigationGroup
    {
        private readonly string _name;
        private readonly List<UiControl> _members = [];
        private UiControl? _entry;
        internal RovingNavigationGroup(FrameworkElement host, string name)
        {
            _name = name;
            AutomationProperties.SetName(host, name);
            AutomationProperties.SetHelpText(host, "Arrow keys or D-pad explore inputs in this group. Tab or RB goes to the next group or control; Shift+Tab or LB goes back. Select inspects an input without performing its Windows action.");
            KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.Once);
            host.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
            {
                if (e.NewFocus is DependencyObject focus && _members.FirstOrDefault(member => NavigationContains(member, focus)) is { } member)
                { _entry = member; ApplyEntry(); }
            }), true);
        }
        internal IReadOnlyList<UiControl> Members => _members;
        internal UiControl? Entry => _entry;
        internal string Name => _name;
        internal bool Contains(DependencyObject target) => _members.Any(member => NavigationContains(member, target));
        internal void Refresh(IEnumerable<UiControl> controls)
        {
            var next = controls.Distinct().ToArray();
            foreach (var removed in _members.Except(next)) KeyboardNavigation.SetIsTabStop(removed, true);
            _members.Clear(); _members.AddRange(next);
            var focused = Keyboard.FocusedElement as DependencyObject;
            var actual = focused == null ? null : _members.FirstOrDefault(member => NavigationContains(member, focused));
            if (actual != null && Eligible(actual)) _entry = actual;
            if (_entry == null || !_members.Contains(_entry))
                _entry = _members.FirstOrDefault(Eligible) ?? _members.FirstOrDefault(member => member.IsEnabled && member.Focusable);
            else if (!Eligible(_entry) && _members.FirstOrDefault(Eligible) is { } replacement)
                _entry = replacement;
            ApplyEntry();
        }
        private static bool Eligible(UiControl member)
        {
            if (!member.IsEnabled || !member.Focusable || member.IsLoaded && !member.IsVisible) return false;
            // Newly rebuilt controls may not have received Loaded yet. Their own
            // visibility must not give a collapsed representation a Tab entry.
            for (DependencyObject? current = member; current != null; current = NavigationParent(current))
                if (current is UIElement element && element.Visibility != Visibility.Visible) return false;
            return true;
        }
        private void ApplyEntry()
        {
            foreach (var member in _members)
                KeyboardNavigation.SetIsTabStop(member, member == _entry && Eligible(member));
        }
        internal void Move(UIElement focused, bool forward)
        {
            var available = _members.Where(AvailableNavigationElement).ToArray();
            if (available.Length == 0) return;
            int index = Array.FindIndex(available, member => NavigationContains(member, focused));
            int next = index < 0 ? 0 : Math.Clamp(index + (forward ? 1 : -1), 0, available.Length - 1);
            FocusNavigationTarget(available[next]);
        }
    }
}

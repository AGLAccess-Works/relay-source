using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControllerMouse.App;

// The settings page shows a readable summary. Choices are made in a separate,
// ordinary button list, so arrows can explore without changing a saved value.
public sealed class SettingChoiceButton : Button
{
    public static readonly DependencyProperty SettingNameProperty = DependencyProperty.Register(
        nameof(SettingName), typeof(string), typeof(SettingChoiceButton),
        new PropertyMetadata("", (owner, _) => ((SettingChoiceButton)owner).RefreshSummary()));
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SettingChoiceButton),
        new PropertyMetadata(null, (owner, _) => ((SettingChoiceButton)owner).RefreshItems()));
    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(SettingChoiceButton),
        new PropertyMetadata("Name", (owner, _) => ((SettingChoiceButton)owner).RefreshSummary()));
    public static readonly DependencyProperty SelectedValuePathProperty = DependencyProperty.Register(
        nameof(SelectedValuePath), typeof(string), typeof(SettingChoiceButton),
        new PropertyMetadata("Value", (owner, _) => ((SettingChoiceButton)owner).RefreshItems()));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(SettingChoiceButton),
        new PropertyMetadata(-1, (owner, change) => ((SettingChoiceButton)owner).SelectIndex((int)change.NewValue)));
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(object), typeof(SettingChoiceButton),
        new PropertyMetadata(null, (owner, change) => ((SettingChoiceButton)owner).SelectValue(change.NewValue)));

    private readonly Grid _summary = new();
    private readonly TextBlock _setting = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _value = new() { FontWeight = FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    private object[] _items = [];
    private object? _selectedItem;
    private bool _syncing;
    private bool? _stackedSummary;
    private string _automationName = "", _automationStatus = "";

    public string SettingName { get => (string)GetValue(SettingNameProperty); set => SetValue(SettingNameProperty, value); }
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string DisplayMemberPath { get => (string)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
    public string SelectedValuePath { get => (string)GetValue(SelectedValuePathProperty); set => SetValue(SelectedValuePathProperty, value); }
    public string DescriptionMemberPath { get; set; } = "Description";
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public object? SelectedValue { get => GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }
    public object? SelectedItem => _selectedItem;
    public IReadOnlyList<object> Items => _items;
    public event SelectionChangedEventHandler? SelectionChanged;

    internal string CurrentDisplayName => _selectedItem == null ? "Choose" : DisplayName(_selectedItem);
    internal SettingChoiceWindow? ActiveChoiceWindow { get; private set; }

    public SettingChoiceButton()
    {
        SetResourceReference(StyleProperty, typeof(Button));
        SetResourceReference(FontSizeProperty, "MainFontSize");
        SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
        SetResourceReference(MinHeightProperty, "ControlMinHeight");
        MinWidth = 44;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _setting.VerticalAlignment = VerticalAlignment.Center;
        _value.VerticalAlignment = VerticalAlignment.Center;
        _value.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        _summary.Children.Add(_setting); _summary.Children.Add(_value);
        Content = _summary;
        AutomationProperties.SetHelpText(this, "Open the choices. Move to an option, then press A or Enter to use it. Cancel or Escape keeps the current choice.");
        SizeChanged += (_, _) => ArrangeSummary();
        Loaded += (_, _) => { RefreshSummary(); ArrangeSummary(); };
        RefreshSummary();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SettingChoiceButtonAutomationPeer(this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty && _summary != null) ArrangeSummary();
    }

    protected override void OnClick()
    {
        base.OnClick();
        OpenChoiceWindow();
    }

    internal void OpenChoiceWindow()
    {
        if (!IsEnabled || !IsVisible || _items.Length == 0 || ActiveChoiceWindow != null || Window.GetWindow(this) is not { } owner) return;
        var choices = new SettingChoiceWindow(owner, this);
        ActiveChoiceWindow = choices;
        try
        {
            if (choices.ShowDialog() == true)
            {
                // Live controller detection can reorder or replace a simple-input
                // list while this owned dialog is open. Match the row's captured
                // logical value, rather than applying its old display position.
                int currentIndex = Array.FindIndex(_items, item => Equals(ChoiceValue(item), choices.SelectedValue));
                if (currentIndex >= 0) SelectedIndex = currentIndex;
            }
        }
        finally
        {
            ActiveChoiceWindow = null;
            // Applying text size can rearrange the page. Return to the same setting
            // after its new layout, rather than losing the reader's place.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!IsVisible || !IsEnabled) return;
                BringIntoView(); Keyboard.Focus(this);
            }));
        }
    }

    internal string DisplayName(object item) => ReadMember(item, DisplayMemberPath)?.ToString() ?? item.ToString() ?? "Choice";
    internal string Description(object item) => ReadMember(item, DescriptionMemberPath)?.ToString() ?? "";
    internal object? ChoiceValue(object item) => string.IsNullOrEmpty(SelectedValuePath) ? item : ReadMember(item, SelectedValuePath);

    private static object? ReadMember(object item, string path)
    {
        if (string.IsNullOrEmpty(path)) return item;
        object? result = item;
        foreach (string member in path.Split('.'))
        {
            if (result == null) return null;
            var property = result.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.GetIndexParameters().Length != 0) return null;
            result = property.GetValue(result);
        }
        return result;
    }

    private void RefreshItems()
    {
        if (_syncing) return;
        object? previousValue = SelectedValue;
        int previousIndex = SelectedIndex;
        _items = ItemsSource?.Cast<object>().Where(item => item != null).ToArray() ?? [];
        int index = previousValue == null ? previousIndex : Array.FindIndex(_items, item => Equals(ChoiceValue(item), previousValue));
        SelectIndex(index);
    }

    private void SelectValue(object? value)
    {
        if (!_syncing) SelectIndex(value == null ? -1 : Array.FindIndex(_items, item => Equals(ChoiceValue(item), value)));
    }

    private void SelectIndex(int index)
    {
        if (_syncing) return;
        if (index < 0 || index >= _items.Length) index = -1;
        object? previous = _selectedItem;
        object? next = index < 0 ? null : _items[index];
        bool changed = !Equals(previous, next);
        _syncing = true;
        try
        {
            _selectedItem = next;
            SetCurrentValue(SelectedIndexProperty, index);
            SetCurrentValue(SelectedValueProperty, next == null ? null : ChoiceValue(next));
        }
        finally { _syncing = false; }
        RefreshSummary();
        if (changed)
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(Selector.SelectionChangedEvent,
                previous == null ? Array.Empty<object>() : new[] { previous },
                next == null ? Array.Empty<object>() : new[] { next })
            { Source = this });
    }

    private void RefreshSummary()
    {
        // Dependency properties can change before the constructor builds content.
        if (_setting == null || _value == null) return;
        _setting.Text = SettingName; _value.Text = CurrentDisplayName;
        string oldName = _automationName, oldStatus = _automationStatus;
        _automationName = $"{SettingName}, {CurrentDisplayName}. Change";
        _automationStatus = "Current choice: " + CurrentDisplayName;
        AutomationProperties.SetName(this, _automationName);
        AutomationProperties.SetItemStatus(this, _automationStatus);
        if (UIElementAutomationPeer.FromElement(this) is { } peer)
        {
            if (oldName != _automationName) peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, _automationName);
            if (oldStatus != _automationStatus) peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, oldStatus, _automationStatus);
        }
        ArrangeSummary();
    }

    private void ArrangeSummary()
    {
        if (_summary == null || _setting == null || _value == null) return;
        bool stacked = ActualWidth > 0 && ActualWidth < Math.Max(360, FontSize * 24);
        if (_stackedSummary == stacked) return;
        _stackedSummary = stacked;
        _summary.ColumnDefinitions.Clear(); _summary.RowDefinitions.Clear();
        _summary.ColumnDefinitions.Add(new() { Width = new GridLength(stacked ? 1 : .54, GridUnitType.Star) });
        if (!stacked) _summary.ColumnDefinitions.Add(new() { Width = new GridLength(.46, GridUnitType.Star) });
        _summary.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (stacked) _summary.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid.SetColumn(_value, stacked ? 0 : 1); Grid.SetRow(_value, stacked ? 1 : 0);
        _setting.Margin = stacked ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 20, 0);
        _value.TextAlignment = stacked ? TextAlignment.Left : TextAlignment.Right;
    }

    private sealed class SettingChoiceButtonAutomationPeer(SettingChoiceButton owner) : ButtonAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => "Relay setting";
        protected override string GetNameCore() => owner._automationName;
        protected override string GetItemStatusCore() => owner._automationStatus;
    }
}

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

// Each option is an ordinary button. Moving focus explores the row; only
// invoking a button changes its value. All options remain in the tab order.
internal sealed class DirectSettingChoiceRow : UserControl
{
    private readonly Grid _layout = new();
    private readonly TextBlock _caption = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly WrapPanel _options = new();
    private readonly List<ChoiceButton> _buttons = [];
    private object[] _items = [];
    private IEnumerable? _source;
    private object? _selectedValue;
    private int _selectedIndex = -1;

    public string SettingName { get; }
    public string DisplayMemberPath { get; set; } = "Name";
    public string SelectedValuePath { get; set; } = "Value";
    public IEnumerable? ItemsSource
    {
        get => _source;
        set
        {
            _source = value;
            object[] next = value?.Cast<object>().Where(item => item != null).ToArray() ?? [];
            // Preference sync recreates equivalent records. Keep the real buttons
            // so changing text size or colours does not detach keyboard focus.
            if (_items.SequenceEqual(next)) return;
            object? previousValue = _selectedValue;
            _items = next; BuildOptions();
            SelectIndex(Array.FindIndex(_items, item => Equals(ChoiceValue(item), previousValue)));
        }
    }
    public int SelectedIndex { get => _selectedIndex; set => SelectIndex(value); }
    public object? SelectedValue
    {
        get => _selectedValue;
        set => SelectIndex(Array.FindIndex(_items, item => Equals(ChoiceValue(item), value)));
    }
    public IReadOnlyList<object> Items => _items;
    public IReadOnlyList<Button> ChoiceButtons => _buttons;
    public string CurrentDisplayName => _selectedIndex < 0 ? "Choose" : DisplayName(_items[_selectedIndex]);
    public event SelectionChangedEventHandler? SelectionChanged;

    internal DirectSettingChoiceRow(string settingName, string automationId)
    {
        SettingName = settingName; _caption.Text = settingName;
        Focusable = false; Margin = new Thickness(0, 4, 0, 4);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        SetResourceReference(FontSizeProperty, "MainFontSize");
        SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
        AutomationProperties.SetName(this, settingName);
        AutomationProperties.SetAutomationId(this, automationId);
        AutomationProperties.SetHelpText(this, "Move between the options. Select an option to apply it.");
        _layout.Children.Add(_caption); _layout.Children.Add(_options); Content = _layout;
        SizeChanged += (_, _) => ArrangeOptions();
        Loaded += (_, _) => ArrangeOptions();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty && _layout != null) ArrangeOptions();
    }

    private void BuildOptions()
    {
        _options.Children.Clear(); _buttons.Clear();
        for (int index = 0; index < _items.Length; index++)
        {
            int optionIndex = index;
            string name = DisplayName(_items[index]);
            var button = new ChoiceButton(SettingName, name)
            {
                MinWidth = 44,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 8, 8, 8),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            button.SetResourceReference(StyleProperty, typeof(Button));
            button.SetResourceReference(MinHeightProperty, "ControlMinHeight");
            button.SetResourceReference(FontSizeProperty, "MainFontSize");
            button.SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
            AutomationProperties.SetAutomationId(button, AutomationProperties.GetAutomationId(this) + "Choice" + index);
            AutomationProperties.SetLabeledBy(button, _caption);
            string description = ReadMember(_items[index], "Description")?.ToString() ?? "";
            AutomationProperties.SetHelpText(button, string.IsNullOrWhiteSpace(description)
                ? "Select to apply this option. Moving focus keeps the current setting."
                : description + " Select to apply this option. Moving focus keeps the current setting.");
            button.Click += (_, _) =>
            {
                SelectedIndex = optionIndex;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    if (button.IsVisible && button.IsKeyboardFocused) button.BringIntoView();
                }));
            };
            button.GotKeyboardFocus += (_, _) => button.BringIntoView();
            _buttons.Add(button); _options.Children.Add(button);
        }
        ArrangeOptions();
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _items.Length) index = -1;
        object? previous = _selectedValue;
        _selectedIndex = index;
        _selectedValue = index < 0 ? null : ChoiceValue(_items[index]);
        for (int i = 0; i < _buttons.Count; i++) _buttons[i].SetSelected(i == index);
        AutomationProperties.SetItemStatus(this, "Current choice: " + CurrentDisplayName);
        if (!Equals(previous, _selectedValue))
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(Selector.SelectionChangedEvent,
                previous == null ? [] : new[] { previous }, _selectedValue == null ? [] : new[] { _selectedValue })
            { Source = this });
    }

    private void ArrangeOptions()
    {
        if (_layout == null || _caption == null || _options == null) return;
        double width = ActualWidth > 0 ? ActualWidth : 640;
        bool stacked = width < Math.Max(560, FontSize * 35);
        double labelWidth = stacked ? 0 : Math.Max(160, FontSize * 11);
        _layout.ColumnDefinitions.Clear(); _layout.RowDefinitions.Clear();
        _layout.ColumnDefinitions.Add(new() { Width = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(labelWidth) });
        if (!stacked) _layout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        _layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (stacked) _layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid.SetColumn(_options, stacked ? 0 : 1); Grid.SetRow(_options, stacked ? 1 : 0);
        _caption.Margin = stacked ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 16, 6);
        double available = Math.Max(44, width - labelWidth);
        double minimumSlot = Math.Max(112, FontSize * 6 + 22);
        int columns = Math.Max(1, Math.Min(_buttons.Count, (int)Math.Floor(available / minimumSlot)));
        double optionWidth = Math.Max(44, available / columns - 6);
        foreach (var button in _buttons) button.Width = optionWidth;
    }

    private string DisplayName(object item) => ReadMember(item, DisplayMemberPath)?.ToString() ?? item.ToString() ?? "Choice";
    private object? ChoiceValue(object item) => string.IsNullOrWhiteSpace(SelectedValuePath) ? item : ReadMember(item, SelectedValuePath);
    private static object? ReadMember(object item, string path) => item.GetType().GetProperty(path, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item);

    private sealed class ChoiceButton : Button
    {
        private readonly string _name;
        private readonly string _option;
        private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        internal string Status { get; private set; } = "Available choice";
        internal ChoiceButton(string setting, string option)
        {
            _name = setting + ", " + option.Replace('\n', ' '); _option = option;
            _text.Text = option; Content = _text;
            AutomationProperties.SetName(this, _name);
        }
        internal void SetSelected(bool selected)
        {
            string previous = Status;
            Status = selected ? "Selected" : "Available choice";
            _text.Text = (selected ? "✓ " : "") + _option;
            _text.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            SetResourceReference(BackgroundProperty, selected ? "SelectionSurfaceBrush" : "SurfaceRaisedBrush");
            SetResourceReference(ForegroundProperty, selected ? "SelectionTextBrush" : "TextPrimaryBrush");
            // This is a single outline against the surface. SelectedInnerBrush
            // is designed to contrast with the outer keyboard-focus ring.
            SetResourceReference(BorderBrushProperty, selected ? "TextPrimaryBrush" : "BorderStrongBrush");
            BorderThickness = new Thickness(selected ? 2 : 1);
            AutomationProperties.SetItemStatus(this, Status);
            if (previous != Status && UIElementAutomationPeer.FromElement(this) is { } peer)
                peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, previous, Status);
        }
        protected override AutomationPeer OnCreateAutomationPeer() => new ChoiceButtonAutomationPeer(this);
        private sealed class ChoiceButtonAutomationPeer(ChoiceButton owner) : ButtonAutomationPeer(owner)
        {
            protected override string GetNameCore() => owner._name;
            protected override string GetItemStatusCore() => owner.Status;
        }
    }
}

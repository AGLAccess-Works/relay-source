using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ControllerMouse.Core;
using Forms = System.Windows.Forms;

namespace ControllerMouse.App;

/// <summary>A normal-integrity keyboard that accepts controller clicks without taking the editor's focus.</summary>
internal sealed class KeyboardWindow : Window
{
    private readonly Func<ushort[], bool> _send;
    private readonly Func<bool> _canType;
    private readonly Func<bool> _fixedSwitchControls;
    private bool _usingSwitchControls;
    private readonly KeyboardModifiers _modifiers = new();
    private readonly Dictionary<ushort, ButtonBase> _keys = new();
    private readonly HashSet<ushort> _characterKeys = [];
    private readonly Grid _rows = new();
    private readonly KeyboardSelection _selection = new();
    private NativeMethods.Point _pointerSelectionAnchor;
    private bool _pointerAnchorValid;
    private bool _symbols;
    private ButtonBase? _pasteButton, _hideButton;
    private static readonly DependencyProperty SelectionActiveProperty = DependencyProperty.RegisterAttached(
        "SelectionActive", typeof(bool), typeof(KeyboardWindow), new PropertyMetadata(false));
    private readonly ScrollViewer _keyViewport = new()
    {
        Focusable = false,
        CanContentScroll = false,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private bool Compact => (ActualWidth > 0 ? ActualWidth : Width) < 950;
    private void AdaptSize()
    {
        _layout.Visibility = Compact ? Visibility.Collapsed : Visibility.Visible;
        double scale = (TryFindResource("MainFontSize") is double size ? size : 16) / 16;
        _rows.MinWidth = 600 * scale; _rows.MinHeight = 240 * scale;
        foreach (var pair in _keys)
        {
            if (pair.Key is 0xfffd or 0xfffe) continue;
            pair.Value.SetResourceReference(FontSizeProperty, Compact ? "SectionFontSize" : "PageTitleFontSize");
            pair.Value.Padding = new Thickness(3, 1, 3, 1);
            if (!_characterKeys.Contains(pair.Key)) SetKeyLabel(pair.Key, pair.Key switch
            {
                0 => _symbols ? "ABC" : "&123",
                0x1b => "Esc",
                0x09 => "Tab",
                0x08 => "Backspace",
                0x0d => "Enter",
                0x10 => "Shift" + (_modifiers.Selected(0x10) ? " ✓" : ""),
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x5b => "Win",
                0x14 => "Caps",
                0x20 => "Space",
                0x25 => "Left",
                0x27 => "Right",
                0x26 => "Up",
                0x28 => "Down",
                0x24 => "Home",
                0x23 => "End",
                0x2e => "Delete",
                0xffff => "Voice",
                _ => ""
            });
        }
    }
    private readonly List<KeyboardKey[]> _selectionRows = [];
    internal bool SymbolsVisible => _symbols;
    internal int SelectedKey => _selection.Selected;
    internal void ControllerCommand(Shortcut command)
    {
        RelayHoverReader.ForWindow(this)?.Cancel();
        if (!IsVisible || !_canType()) return;
        AnchorPointerSelection();
        int previous = _selection.Selected;
        switch (command)
        {
            case Shortcut.KeyboardUp: _selection.Move(0, -1); break;
            case Shortcut.KeyboardDown: _selection.Move(0, 1); break;
            case Shortcut.KeyboardLeft: _selection.Move(-1, 0); break;
            case Shortcut.KeyboardRight: _selection.Move(1, 0); break;
            case Shortcut.KeyboardNext: MoveSequentialKey(1); break;
            case Shortcut.KeyboardPrevious: MoveSequentialKey(-1); break;
            case Shortcut.KeyboardSelect:
                var selected = _keys[(ushort)_selection.Selected];
                if (!selected.IsEnabled) { _notice.Text = "This key is not available in the current Windows keyboard layout."; return; }
                selected.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); return;
            case Shortcut.KeyboardSpace: PressKey(0x20); return;
            case Shortcut.KeyboardBackspace: PressKey(0x08); return;
            case Shortcut.KeyboardSymbols: ToggleSymbols(); return;
            case Shortcut.KeyboardShift: PressKey(0x10); return;
            default: return;
        }
        RefreshSelection();
        _keys[(ushort)_selection.Selected].BringIntoView();
        if (_selection.Selected != previous) AnnounceSelectedKey();
    }
    private void MoveSequentialKey(int direction)
    {
        int origin = _selection.Selected;
        do { _selection.MoveSequential(direction); }
        while (!_keys[(ushort)_selection.Selected].IsEnabled && _selection.Selected != origin);
    }
    private void AnnounceSelectedKey()
    {
        var key = _keys[(ushort)_selection.Selected];
        _notice.Text = AutomationProperties.GetName(key) + (key is ToggleButton toggle ? (toggle.IsChecked == true ? ", on." : ", off.") : ".") + " Press A to choose.";
    }
    private void ToggleSymbols()
    {
        AnchorPointerSelection();
        _symbols = !_symbols; BuildKeys(0); RefreshLabels(true);
        _notice.Text = (_symbols ? "Numbers and symbols." : "Letters.") + (_usingSwitchControls
            ? " B moves to the next key. X moves to the previous key. A selects it. Y hides the keyboard."
            : " D-pad chooses a key. A selects it.");
    }
    private void RefreshSelection()
    {
        foreach (var pair in _keys)
        {
            bool selected = pair.Key == _selection.Selected;
            pair.Value.SetValue(SelectionActiveProperty, selected);
            pair.Value.BorderThickness = new Thickness(1);
            pair.Value.SetResourceReference(BorderBrushProperty, "LineBrush");
            bool on = pair.Value is ToggleButton toggle && toggle.IsChecked == true;
            AutomationProperties.SetItemStatus(pair.Value, (selected ? "Selected key. " : "") + (pair.Value is ToggleButton ? (on ? "On" : "Off") : ""));
        }
    }
    private void AnchorPointerSelection()
    {
        _pointerAnchorValid = NativeMethods.GetPhysicalCursorPos(out _pointerSelectionAnchor);
    }
    private void SelectPointerKey(ushort key)
    {
        if (!IsVisible || !NativeMethods.GetPhysicalCursorPos(out var pointer)) return;
        if (!_pointerAnchorValid) { _pointerSelectionAnchor = pointer; _pointerAnchorValid = true; return; }
        // Showing or rebuilding a key beneath a stationary pointer raises MouseEnter.
        // Only actual pointer movement may replace a controller's selected key.
        if (pointer.X == _pointerSelectionAnchor.X && pointer.Y == _pointerSelectionAnchor.Y) return;
        _pointerSelectionAnchor = pointer;
        if (!_keys.ContainsKey(key)) return;
        _selection.Select(key); RefreshSelection();
    }
    private void EnablePointerSelection(ButtonBase button, ushort key)
    {
        button.MouseEnter += (_, _) => SelectPointerKey(key);
        button.MouseMove += (_, _) => SelectPointerKey(key);
    }
    private void SetKeyLabel(ushort key, string label)
    {
        var button = _keys[key];
        if (Compact) label = key switch { 0x08 => "Back", 0x0d => "Enter", 0x25 => "←", 0x27 => "→", 0x26 => "↑", 0x28 => "↓", 0xffff => "Voice", _ => label };
        string? hint = key switch { 0 => "LT", 0x10 => "RT", 0x20 when !_usingSwitchControls => "Y", 0x25 => "LB", 0x27 => "RB", 0x08 when !_usingSwitchControls => "X", 0xffff => "Win + H", _ => null };
        if (hint == null)
        {
            var glyph = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            glyph.SetResourceReference(TextBlock.FontSizeProperty, Compact ? "SectionFontSize" : "PageTitleFontSize");
            button.Content = glyph; return;
        }
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var keyLabel = new TextBlock { Text = label, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        keyLabel.SetResourceReference(TextBlock.FontSizeProperty, Compact || key is 0 or 0xffff ? "SectionFontSize" : "PageTitleFontSize");
        var shortcut = new TextBlock { Text = hint, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        shortcut.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        panel.Children.Add(keyLabel); panel.Children.Add(shortcut);
        button.Content = panel;
    }
    private void BuildKeys(int selected = 0x1b)
    {
        RelayHoverReader.ForWindow(this)?.Cancel();
        _rows.Children.Clear(); _rows.RowDefinitions.Clear(); _keys.Clear(); _characterKeys.Clear(); _selectionRows.Clear();
        for (int i = 0; i < 4; i++) _rows.RowDefinitions.Add(new());
        if (!_symbols)
        {
            AddRow(_rows, 0, [new(0x1b, "Esc", 1.3), .. "QWERTYUIOP".Select(c => new KeySpec(c)), new(0x08, "Backspace", 1.3)]);
            AddRow(_rows, 1, [new(0x09, "Tab", 1.3), .. "ASDFGHJKL".Select(c => new KeySpec(c)), new(0xde), new(0x0d, "Enter", 1.3)]);
            AddRow(_rows, 2, [new(0x10, "Shift", 1.3), .. "ZXCVBNM".Select(c => new KeySpec(c)), new(0xbc), new(0xbe), new(0xbf), new(0x26, "Up")]);
        }
        else
        {
            AddRow(_rows, 0, [new(0x1b, "Esc", 1.3), .. "1234567890".Select(c => new KeySpec(c)), new(0x08, "Backspace", 1.3)]);
            AddRow(_rows, 1, [new(0x09, "Tab", 1.3), new(0xc0), new(0xbd), new(0xbb), new(0xdb), new(0xdd), new(0xdc), new(0xba), new(0xde), new(0xe2), new(0x0d, "Enter", 1.3)]);
            AddRow(_rows, 2, [new(0x10, "Shift", 1.3), new(0x14, "Caps"), new(0xbc), new(0xbe), new(0xbf), new(0x26, "Up"), new(0x28, "Down"), new(0x24, "Home"), new(0x23, "End"), new(0x2e, "Delete")]);
        }
        AddRow(_rows, 3, [new(0, _symbols ? "ABC" : "&123", 1.3), new(0x11, "Ctrl"), new(0x5b, "Win"), new(0x12, "Alt"), new(0x20, "Space", 5), new(0xffff, "Voice"), new(0x25, "Left"), new(0x27, "Right")]);
        if (_pasteButton != null && _hideButton != null)
        {
            _keys.Add(0xfffe, _pasteButton); _keys.Add(0xfffd, _hideButton);
            _selectionRows.Insert(0, [new(0xfffe), new(0xfffd)]);
        }
        _selection.SetRows(_selectionRows.ToArray(), selected); AdaptSize(); RefreshSelection();
    }
    private readonly TextBlock _layout = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 0, 0) };
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private HwndSource? _source;
    private IntPtr _lastLayout, _lastTarget;
    private bool _lastCaps;
    internal ButtonBase KeyButton(ushort key) => _keys[key];
    internal IEnumerable<ButtonBase> AllKeyButtons => _keys.Values;
    internal bool ModifierSelected(ushort key) => _modifiers.Selected(key);
    internal void ClearPendingModifiers() { _modifiers.Clear(); RefreshLabels(true); }

    internal KeyboardWindow(Func<ushort[], bool> send, Func<bool>? canType = null, Func<bool>? fixedSwitchControls = null)
    {
        _send = send; _canType = canType ?? (() => true);
        _fixedSwitchControls = fixedSwitchControls ?? (() => Application.Current is App { Service.FixedSwitchKeyboardLayout: true });
        _usingSwitchControls = _fixedSwitchControls();
        Title = "Relay keyboard"; Width = 1280; Height = 500; MinWidth = 640; MinHeight = 340;
        AutomationProperties.SetName(this, Title);
        UpdateControlHelp();
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowActivated = false; ShowInTaskbar = false; Topmost = true; Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        SetResourceReference(FontFamilyProperty, "InterfaceFontFamily"); SetResourceReference(FontSizeProperty, "MainFontSize");
        _layout.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        _notice.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        var surface = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(12) };
        surface.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(4, 0, 4, 10), Background = Brushes.Transparent };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var title = new TextBlock { Text = "Relay keyboard", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        title.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        title.ToolTip = "Drag to move the keyboard"; header.Children.Add(title);
        AutomationProperties.SetAutomationId(_layout, "RelayKeyboardLayout");
        _layout.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); _layout.Margin = new Thickness(12, 0, 18, 0);
        Grid.SetColumn(_layout, 1); header.Children.Add(_layout);
        var close = new Button { Content = _usingSwitchControls ? "Hide (Y)" : "Hide", Focusable = false, MinHeight = 44, Padding = new Thickness(14, 6, 14, 6), Template = KeyTemplate() };
        AutomationProperties.SetName(close, "Hide Relay keyboard"); AutomationProperties.SetAutomationId(close, "RelayKeyboardHide"); close.Click += (_, _) => Hide();
        var paste = new Button { Content = "Paste", Focusable = false, MinHeight = 44, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(14, 6, 14, 6), Template = KeyTemplate() };
        AutomationProperties.SetName(paste, "Paste clipboard into active text field");
        AutomationProperties.SetAutomationId(paste, "RelayKeyboardPaste");
        paste.Click += (_, _) =>
        {
            if (!_canType()) { _notice.Text = "Resume Relay to paste."; return; }
            _modifiers.Clear();
            _notice.Text = _send([0x11, 0x56]) ? "Clipboard paste sent to the active window." : "Paste was not sent. Select a text field first.";
            RefreshLabels(true);
        };
        _pasteButton = paste; _hideButton = close;
        EnablePointerSelection(paste, 0xfffe);
        EnablePointerSelection(close, 0xfffd);
        Grid.SetColumn(paste, 2); header.Children.Add(paste);
        Grid.SetColumn(close, 3); header.Children.Add(close);
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock) { DragMove(); e.Handled = true; } };
        root.Children.Add(header);

        BuildKeys();
        _keyViewport.Content = _rows; Grid.SetRow(_keyViewport, 1); root.Children.Add(_keyViewport);
        _notice.Text = ControllerGuide;
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        AutomationProperties.SetAutomationId(_notice, "RelayKeyboardStatus");
        AutomationProperties.SetLiveSetting(_notice, AutomationLiveSetting.Polite);
        AccessibilitySupport.AnnounceChanges(_notice, TimeSpan.FromMilliseconds(180));
        Grid.SetRow(_notice, 2); root.Children.Add(_notice); surface.Child = root; Content = surface;
        // Hover speech must not activate this window or move focus out of the editor.
        RelayHoverReader.Attach(this, () => _canType());
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            long style = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
            NativeMethods.SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x08000000 | 0x80)); // NOACTIVATE | TOOLWINDOW
            _source = HwndSource.FromHwnd(hwnd); _source?.AddHook(WindowHook);
        };
        IsVisibleChanged += (_, _) =>
        {
            RelayHoverReader.ForWindow(this)?.Cancel();
            AnchorPointerSelection();
            _modifiers.Clear(); _lastTarget = NativeMethods.GetForegroundWindow(); RefreshLabels(true);
            if (IsVisible)
            {
                _selection.Select(0x1b); RefreshSelection();
                _notice.Text = "Keyboard ready. Escape selected. " + ControllerGuide;
                _refresh.Start();
            }
            else _refresh.Stop();
        };
        _refresh.Tick += (_, _) => RefreshLabels();
        SizeChanged += (_, _) => AdaptSize();
        Closed += (_, _) => { RelayHoverReader.ForWindow(this)?.Cancel(); _refresh.Stop(); _source?.RemoveHook(WindowHook); _modifiers.Clear(); };
    }
    private string ControllerGuide => _usingSwitchControls
        ? "A: select. B: next key. X: previous key. Y: hide. Choose Space, Backspace or &123 on the keyboard when needed."
        : "D-pad: choose a key. A: type. Y: space. X: erase. B: hide. Move the left stick to use the mouse.";
    private void UpdateControlHelp()
    {
        AutomationProperties.SetHelpText(this, "Choose a text field first. This keyboard keeps that text field active. " + ControllerGuide);
        if (_hideButton != null) _hideButton.Content = _usingSwitchControls ? "Hide (Y)" : "Hide";
    }
    private void RefreshControlHelp()
    {
        bool switches = _fixedSwitchControls();
        if (switches == _usingSwitchControls) return;
        _usingSwitchControls = switches; UpdateControlHelp(); AdaptSize();
        _notice.Text = ControllerGuide;
    }

    private sealed record KeySpec(int Key, string? Label = null, double Width = 1);
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty && _rows != null) AdaptSize();
    }
    private static ControlTemplate KeyTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var surface = new FrameworkElementFactory(typeof(Border)); surface.Name = "KeySurface";
        surface.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        surface.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        surface.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        surface.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(ForegroundProperty));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        surface.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentProperty));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); surface.AppendChild(content); root.AppendChild(surface);
        foreach (var (name, token, thickness, inset) in new[] {
            ("KeySelectionOuter", "FocusRingBrush", "FocusOuterThickness", false),
            ("KeySelectionInner", "FocusInnerBrush", "FocusInnerThickness", true) })
        {
            var ring = new FrameworkElementFactory(typeof(Border)); ring.Name = name;
            ring.SetResourceReference(Border.BorderBrushProperty, token); ring.SetResourceReference(Border.BorderThicknessProperty, thickness);
            if (inset) ring.SetResourceReference(MarginProperty, "FocusOuterThickness");
            ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            ring.SetValue(IsHitTestVisibleProperty, false); ring.SetValue(VisibilityProperty, Visibility.Collapsed); root.AppendChild(ring);
        }
        var template = new ControlTemplate(typeof(ButtonBase)) { VisualTree = root };
        var selected = new Trigger { Property = SelectionActiveProperty, Value = true };
        selected.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "KeySelectionOuter"));
        selected.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "KeySelectionInner")); template.Triggers.Add(selected);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SelectionSurfaceBrush"), "KeySurface"));
        // Override inherited modifier ink together with the pressed fill. A latched modifier
        // normally has light ink on purple, which is unreadable on the pale pressed surface.
        pressed.Setters.Add(new Setter(TextElement.ForegroundProperty, new DynamicResourceExtension("SelectionTextBrush"), "KeySurface"));
        template.Triggers.Add(pressed);
        return template;
    }
    // WPF's default Toggle provider changes IsChecked without raising Click.
    // Route accessibility activation through the same key handler as mouse/controller activation.
    private sealed class ModifierKeyButton(Action activate) : ToggleButton
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new ModifierKeyPeer(this, activate);
    }
    private sealed class ModifierKeyPeer(ModifierKeyButton button, Action activate) : ToggleButtonAutomationPeer(button), IToggleProvider
    {
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Toggle ? this : base.GetPattern(pattern);
        void IToggleProvider.Toggle()
        {
            if (!button.IsEnabled) throw new ElementNotEnabledException();
            activate();
        }
        ToggleState IToggleProvider.ToggleState => button.IsChecked == true ? ToggleState.On : ToggleState.Off;
    }
    private void AddRow(Grid rows, int index, KeySpec[] specs)
    {
        var row = new Grid(); Grid.SetRow(row, index);
        foreach (var spec in specs)
        {
            row.ColumnDefinitions.Add(new() { Width = new GridLength(spec.Width, GridUnitType.Star) });
            ushort key = (ushort)spec.Key;
            bool modifier = key is 0x10 or 0x11 or 0x12 or 0x5b or 0x14;
            ButtonBase button = key == 0x08 ? new RepeatButton { Delay = 450, Interval = 90 } : modifier ? new ModifierKeyButton(() => PressKey(key)) : new Button();
            button.Style = modifier ? new Style(typeof(ToggleButton), (Style)FindResource("KeyboardKeyBase"))
                : (Style)FindResource(key == 0x08 ? "KeyboardRepeatKey" : "KeyboardKey");
            button.Focusable = false; button.Template = KeyTemplate(); button.Content = spec.Label ?? ((char)key).ToString();
            button.Tag = key;
            AutomationProperties.SetName(button, key switch { 0x26 => "Up arrow", 0x28 => "Down arrow", 0x25 => "Left arrow", 0x27 => "Right arrow", 0x14 => "Caps lock", 0x11 => "Control", 0x5B => "Windows", 0x1B => "Escape", _ => spec.Label ?? ((char)key).ToString() });
            AutomationProperties.SetAutomationId(button, "RelayKeyboardKey" + key.ToString("X4", CultureInfo.InvariantCulture));
            AutomationProperties.SetHelpText(button, modifier ? "Applies to the next key only. Select again to cancel." : "Types into the text field you selected before opening the keyboard.");
            Grid.SetColumn(button, row.ColumnDefinitions.Count - 1); row.Children.Add(button);
            if (spec.Label == null) _characterKeys.Add(key);
            _keys.Add(key, button); SetKeyLabel(key, spec.Label ?? ((char)key).ToString());
            if (key == 0) AutomationProperties.SetName(button, _symbols ? "Show letters" : "Show numbers and symbols");
            if (key == 0xffff) AutomationProperties.SetName(button, "Windows voice typing");
            if (key == 0) AutomationProperties.SetHelpText(button, "Changes the keyboard page. It does not type a character.");
            if (key == 0xffff) AutomationProperties.SetHelpText(button, "Starts Windows voice typing for the text field you selected. Windows manages microphone permission.");
            EnablePointerSelection(button, key);
            button.Click += (_, _) => PressKey((ushort)button.Tag);
        }
        _selectionRows.Add(specs.Select(k => new KeyboardKey(k.Key, k.Width)).ToArray());
        rows.Children.Add(row);
    }
    private static uint ScanCode(ushort key)
    {
        foreach (var row in new[] { ("1234567890", 0x02), ("QWERTYUIOP", 0x10), ("ASDFGHJKL", 0x1E), ("ZXCVBNM", 0x2C) })
        {
            int index = row.Item1.IndexOf((char)key); if (index >= 0) return (uint)(row.Item2 + index);
        }
        return key switch
        {
            0xC0 => 0x29,
            0xBD => 0x0C,
            0xBB => 0x0D,
            0xDB => 0x1A,
            0xDD => 0x1B,
            0xDC => 0x2B,
            0xBA => 0x27,
            0xDE => 0x28,
            0xE2 => 0x56,
            0xBC => 0x33,
            0xBE => 0x34,
            0xBF => 0x35,
            _ => 0
        };
    }
    private static string SpokenCharacter(string label) => label switch
    {
        "." => "Period",
        "," => "Comma",
        "-" => "Hyphen",
        "_" => "Underscore",
        "=" => "Equals",
        "+" => "Plus",
        "/" => "Forward slash",
        "\\" => "Backslash",
        ";" => "Semicolon",
        ":" => "Colon",
        "'" => "Apostrophe",
        "\"" => "Quotation mark",
        "?" => "Question mark",
        "!" => "Exclamation mark",
        "@" => "At sign",
        "#" => "Hash",
        "$" => "Dollar sign",
        "%" => "Percent",
        "^" => "Caret",
        "&" => "Ampersand",
        "*" => "Asterisk",
        "(" => "Left parenthesis",
        ")" => "Right parenthesis",
        "[" => "Left square bracket",
        "]" => "Right square bracket",
        "{" => "Left brace",
        "}" => "Right brace",
        "<" => "Less than",
        ">" => "Greater than",
        "|" => "Vertical bar",
        "~" => "Tilde",
        "`" => "Backtick",
        _ => label
    };
    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE: deliver click, keep editor foreground.
        return IntPtr.Zero;
    }
    private void PressKey(ushort key)
    {
        if (key == 0) { ToggleSymbols(); return; }
        if (key == 0xffff)
        {
            _modifiers.Clear();
            _notice.Text = !_canType() ? "Resume Relay to use voice typing." : _send([0x5b, 0x48])
                ? "Windows voice typing requested. Windows manages microphone permission."
                : "Voice typing was not sent. Select a text window and resume Relay.";
            RefreshLabels(true); return;
        }
        RefreshLabels();
        if (!_canType()) { _modifiers.Clear(); _notice.Text = "Resume Relay to type."; RefreshLabels(true); return; }
        if (key is 0x10 or 0x11 or 0x12 or 0x5B)
        {
            _modifiers.Toggle(key);
            string name = key switch { 0x10 => "Shift", 0x11 => "Control", 0x12 => "Alt", _ => "Windows" };
            _notice.Text = name + (_modifiers.Selected(key) ? " on for the next key. Select again to cancel." : " off.");
        }
        else
        {
            var chord = _modifiers.Consume(key);
            var foreground = NativeMethods.GetForegroundWindow();
            bool accepted = foreground != IntPtr.Zero && foreground != new WindowInteropHelper(this).Handle && _send(chord);
            _notice.Text = accepted ? "Typing into the active window." : "Input was not sent. Select a normal text window and check that Relay is resumed.";
        }
        RefreshLabels(true);
    }
    private void RefreshLabels(bool force = false)
    {
        RefreshControlHelp();
        var target = NativeMethods.GetForegroundWindow();
        if (_lastTarget != target || !_canType()) { _modifiers.Clear(); _lastTarget = target; force = true; }
        var layout = NativeMethods.GetKeyboardLayout(NativeMethods.GetWindowThreadProcessId(target, out _));
        bool caps = (NativeMethods.GetKeyState(0x14) & 1) != 0;
        if (!force && layout == _lastLayout && caps == _lastCaps) return;
        _lastLayout = layout; _lastCaps = caps;
        var state = new byte[256]; state[0x14] = caps ? (byte)1 : (byte)0;
        if (_modifiers.Selected(0x10)) state[0x10] = 0x80;
        // Ctrl+Alt labels expose AltGr characters on layouts that use them.
        if (_modifiers.Selected(0x11) && _modifiers.Selected(0x12)) state[0x11] = state[0x12] = 0x80;
        foreach (var key in _characterKeys)
        {
            uint scan = ScanCode(key);
            ushort virtualKey = (ushort)NativeMethods.MapVirtualKeyEx(scan, 3, layout);
            var buffer = new StringBuilder(16);
            int count = NativeMethods.ToUnicodeEx(virtualKey, scan, state, buffer, 16, 4, layout);
            if (count == 0 || buffer.ToString().Any(char.IsControl))
            {
                var plainState = (byte[])state.Clone(); plainState[0x11] = plainState[0x12] = 0;
                buffer.Clear(); count = NativeMethods.ToUnicodeEx(virtualKey, scan, plainState, buffer, 16, 4, layout);
            }
            string label = count == 0 ? "—" : buffer.ToString(0, Math.Min(Math.Abs(count), buffer.Length));
            _keys[key].Tag = virtualKey;
            _keys[key].IsEnabled = virtualKey != 0 && count != 0;
            SetKeyLabel(key, label);
            AutomationProperties.SetName(_keys[key], _keys[key].IsEnabled ? SpokenCharacter(label) + (count < 0 ? " accent key" : "") : "Unavailable key");
        }
        foreach (ushort key in new ushort[] { 0x10, 0x11, 0x12, 0x5B, 0x14 })
        {
            if (!_keys.TryGetValue(key, out var modifierButton)) continue;
            bool selected = key == 0x14 ? caps : _modifiers.Selected(key);
            if (modifierButton is ToggleButton toggle) toggle.IsChecked = selected;
            AutomationProperties.SetItemStatus(_keys[key], selected ? "On" : "Off");
            AutomationProperties.SetHelpText(_keys[key], key == 0x14 ? "Toggle Caps lock." : "Applies to the next key only. Select again to cancel.");
            SetKeyLabel(key, (key switch { 0x10 => "Shift", 0x11 => "Ctrl", 0x12 => "Alt", 0x5B => "Win", _ => "Caps" }) + (selected ? " ✓" : ""));
            _keys[key].SetResourceReference(BackgroundProperty, selected ? "AccentBrush" : "ControlBrush");
            _keys[key].SetResourceReference(ForegroundProperty, selected ? "OnAccentBrush" : "TextBrush");
        }
        RefreshSelection();
        try { _layout.Text = CultureInfo.GetCultureInfo((int)(layout.ToInt64() & 0xFFFF)).DisplayName; }
        catch (CultureNotFoundException) { _layout.Text = "Windows keyboard layout"; }
    }
    internal void ShowNearPointer()
    {
        AnchorPointerSelection();
        NativeMethods.GetPhysicalCursorPos(out var cursor);
        var area = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)).WorkingArea;
        Show();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double textScale = (TryFindResource("MainFontSize") is double size ? size : 16) / 16;
        int width = Math.Min(area.Width - 24, (int)(1600 * scale));
        int height = Math.Min((int)(area.Height * Math.Min(.8, .55 * textScale)), (int)(500 * scale * textScale));
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1),
            area.Left + (area.Width - width) / 2, area.Bottom - height - 12, width, height, 0x0010);
    }
}

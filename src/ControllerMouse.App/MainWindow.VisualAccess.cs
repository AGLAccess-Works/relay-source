using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private sealed record VisualChoice<T>(string Name, T Value, string Description = "");
    private SettingChoiceButton? _schemeChoice, _highlightChoice;
    private DirectSettingChoiceRow? _themeChoice, _textSizeChoice, _spacingChoice, _complexityChoice, _motionChoice, _focusChoice;
    private CheckBox? _oneControlChoice;
    private TextBox? _customHighlight, _customBackground, _customSurface, _customText, _customSecondary;
    private StackPanel? _customPaletteHost, _customHighlightHost;
    private TextBlock? _visualAccessNotice, _systemTextSizeNotice;
    private Expander? _moreVisualOptions;
    private bool _visualAccessReady, _syncingVisualAccess;

    private void InitializeVisualAccess()
    {
        _app.ApplyVisualAccess(_settings);
        var heading = PresentationText("Display and reading", "SectionFontSize");
        heading.FontWeight = FontWeights.SemiBold;
        AccessibilitySupport.SectionHeading(heading);
        var overview = new StackPanel();
        overview.Children.Add(heading);
        var card = new Border { Style = (Style)FindResource("Card"), MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left, Child = overview };
        VisualAccessHost.Children.Add(card);
        _themeChoice = DirectVisualChoiceField(overview, "Theme", "VisualAccessTheme", new[] {
            new VisualChoice<bool>("Light", true), new("Dark", false) });
        _themeChoice.SelectionChanged += (_, _) =>
        {
            if (!CanChangeVisualAccess || _themeChoice.SelectedValue is not bool light) return;
            var visual = _settings.EffectiveVisualAccess;
            // Theme changes only appearance. Reading and motor preferences remain
            // independent, including a simplified view or a larger focus outline.
            var scheme = visual.Scheme is DisplayScheme.MaximumContrastLight or DisplayScheme.MaximumContrastDark
                ? light ? DisplayScheme.MaximumContrastLight : DisplayScheme.MaximumContrastDark
                : light ? DisplayScheme.RelayDefault : DisplayScheme.DeepDark;
            _settings = _settings with { VisualAccess = visual with { Scheme = scheme }, LightTheme = light };
            CommitVisualAccess(light ? "Light theme applied." : "Dark theme applied.");
        };
        _systemTextSizeNotice = PresentationText("", "CaptionFontSize");
        AutomationProperties.SetAutomationId(_systemTextSizeNotice, "WindowsTextSizeNotice");
        AccessibilitySupport.AnnounceChanges(_systemTextSizeNotice);
        overview.Children.Add(_systemTextSizeNotice);
        UpdateSystemTextSizeNotice();
        _textSizeChoice = DirectVisualChoiceField(overview, "Text size", "VisualAccessTextSize", new[] {
            new VisualChoice<double>("Standard\n100%",1), new("125%",1.25), new("Large\n150%",1.5),
            new("175%",1.75), new("Extra large\n200%",2) });
        _spacingChoice = DirectVisualChoiceField(overview, "Interface spacing", "VisualAccessSpacing", new[] {
            new VisualChoice<VisualSpacing>("Standard",VisualSpacing.Standard), new("Comfortable",VisualSpacing.Comfortable),new("Wide",VisualSpacing.Wide) });
        _complexityChoice = DirectVisualChoiceField(overview, "Visual complexity", "VisualAccessComplexity", new[] {
            new VisualChoice<VisualComplexity>("Standard",VisualComplexity.Standard,"Show the controller picture and all input details."),
            new("Reduced",VisualComplexity.Reduced,"Hide extra labels, captions and technical details."),
            new("Minimal",VisualComplexity.Minimal,"Use a large input card instead of the controller picture.") });
        _focusChoice = DirectVisualChoiceField(overview, "Focus display", "VisualAccessFocus", new[] {
            new VisualChoice<VisualFocus>("Standard",VisualFocus.Standard),new("Strong",VisualFocus.Strong),new("Very strong",VisualFocus.VeryStrong) });
        var more = new StackPanel();
        _moreVisualOptions = new Expander { Header = "More display options", Content = more, Margin = new Thickness(0, 12, 0, 0) };
        AutomationProperties.SetAutomationId(_moreVisualOptions, "VisualAccessMoreOptions");
        AutomationProperties.SetName(_moreVisualOptions, "More display options");
        _schemeChoice = VisualChoiceField(more, "Display scheme", "VisualAccessScheme", new[] {
            new VisualChoice<DisplayScheme>("Relay Default", DisplayScheme.RelayDefault,"Neutral light panels with dark text."),
            new("Soft Light", DisplayScheme.SoftLight,"Soft light panels with dark text."), new("Deep Dark", DisplayScheme.DeepDark,"Dark panels with light text."),
            new("Maximum Contrast Light", DisplayScheme.MaximumContrastLight,"Light panels with dark text and strong outlines."),
            new("Maximum Contrast Dark", DisplayScheme.MaximumContrastDark,"Dark panels with light text and strong outlines."),
            new("CVI Simplified", DisplayScheme.CviSimplified,"Larger text, less detail and one input at a time. You can adjust each setting."),
            new("Custom", DisplayScheme.Custom,"Choose your own colours in More display options.") });
        _schemeChoice.SelectionChanged += (_, _) =>
        {
            if (!CanChangeVisualAccess || _schemeChoice.SelectedValue is not DisplayScheme scheme) return;
            _settings = _settings.WithVisualAccessScheme(scheme);
            CommitVisualAccess("Display scheme changed. You can adjust each preference below.");
        };
        _motionChoice = DirectVisualChoiceField(more, "Movement on screen", "VisualAccessMotion", new[] {
            new VisualChoice<VisualMotion>("On",VisualMotion.Standard),new("Reduced",VisualMotion.Reduced),new("Off",VisualMotion.Off) });
        _highlightChoice = VisualChoiceField(more, "Highlight colour", "VisualAccessHighlight", new[] {
            new VisualChoice<VisualHighlight>("AGL Yellow",VisualHighlight.AglYellow),new("Blue",VisualHighlight.Blue),new("Green",VisualHighlight.Green),
            new("Red",VisualHighlight.Red),new("Orange",VisualHighlight.Orange),new("Purple",VisualHighlight.Purple),new("Custom",VisualHighlight.Custom) });
        foreach (var choice in new[] { _textSizeChoice, _spacingChoice, _complexityChoice, _motionChoice, _focusChoice })
            choice.SelectionChanged += (_, _) => ChangeIndividualVisualPreferences();
        _highlightChoice.SelectionChanged += (_, _) => ChangeIndividualVisualPreferences();
        _oneControlChoice = new CheckBox { Content = "One control at a time (CVI simplified)", Margin = new Thickness(0, 10, 0, 8) };
        AutomationProperties.SetName(_oneControlChoice, "One control at a time (CVI simplified)");
        AutomationProperties.SetAutomationId(_oneControlChoice, "VisualAccessOneControl");
        AutomationProperties.SetHelpText(_oneControlChoice, "Show one large input with Previous and Next buttons. This works with any display scheme.");
        _oneControlChoice.Checked += (_, _) => ChangeIndividualVisualPreferences(); _oneControlChoice.Unchecked += (_, _) => ChangeIndividualVisualPreferences();
        overview.Children.Add(_oneControlChoice);
        overview.Children.Add(_moreVisualOptions);
        _customHighlightHost = new StackPanel();
        _customHighlight = VisualColourField(_customHighlightHost, "Your highlight colour", "VisualAccessCustomHighlight");
        more.Children.Add(_customHighlightHost);
        _customPaletteHost = new StackPanel();
        _customPaletteHost.Children.Add(PresentationText("Your colours", "SectionFontSize"));
        _customBackground = VisualColourField(_customPaletteHost, "Background colour", "VisualAccessCustomBackground");
        _customSurface = VisualColourField(_customPaletteHost, "Panel colour", "VisualAccessCustomSurface");
        _customText = VisualColourField(_customPaletteHost, "Text colour", "VisualAccessCustomText");
        _customSecondary = VisualColourField(_customPaletteHost, "Supporting text colour", "VisualAccessCustomSecondary");
        more.Children.Add(_customPaletteHost);
        var colours = PresentationButton("Apply my colours", "VisualAccessApplyColours", ApplyCustomVisualColours);
        _customPaletteHost.Children.Add(colours);
        _customHighlightHost.Children.Add(PresentationButton("Apply highlight colour", "VisualAccessApplyHighlight", ApplyCustomVisualColours));
        more.Children.Add(PresentationButton("Restore Relay display defaults", "VisualAccessRestore", () =>
        {
            _settings = _settings.WithVisualAccessScheme(DisplayScheme.RelayDefault);
            CommitVisualAccess("Relay display defaults restored. Your text size and controller assignments are kept.");
        }));
        _visualAccessNotice = PresentationText("", "CaptionFontSize", "MutedBrush");
        AutomationProperties.SetAutomationId(_visualAccessNotice, "VisualAccessStatus");
        AccessibilitySupport.AnnounceChanges(_visualAccessNotice); overview.Children.Add(_visualAccessNotice);
        _visualAccessReady = true;
        PopulateVisualAccessOptions();
        InitializeSimpleControls();
        RegisterSectionHeadings();
    }

    private SettingChoiceButton VisualChoiceField<T>(Panel host, string label, string id, VisualChoice<T>[] choices)
    {
        var field = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        var choice = new SettingChoiceButton { SettingName = label, ItemsSource = choices, DisplayMemberPath = "Name", SelectedValuePath = "Value", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(choice, label); AutomationProperties.SetAutomationId(choice, id);
        field.Children.Add(choice); host.Children.Add(field); return choice;
    }
    private DirectSettingChoiceRow DirectVisualChoiceField<T>(Panel host, string label, string id, VisualChoice<T>[] choices)
    {
        var choice = new DirectSettingChoiceRow(label, id) { ItemsSource = choices };
        host.Children.Add(choice); return choice;
    }
    private TextBox VisualColourField(Panel host, string label, string id)
    {
        var caption = PresentationText(label); caption.Margin = new Thickness(0, 8, 0, 6);
        var value = new TextBox { MaxWidth = 280, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 120 };
        AutomationProperties.SetName(value, label); AutomationProperties.SetAutomationId(value, id); AutomationProperties.SetLabeledBy(value, caption);
        AutomationProperties.SetHelpText(value, "Use a colour such as #FFC629. Relay keeps text and focus readable if a colour needs adjustment.");
        host.Children.Add(caption); host.Children.Add(value); return value;
    }
    private bool CanChangeVisualAccess => _visualAccessReady && !_loading && !_syncingVisualAccess;
    private void PopulateVisualAccessOptions()
    {
        if (!_visualAccessReady) return;
        _syncingVisualAccess = true;
        try
        {
            var visual = _settings.EffectiveVisualAccess;
            _themeChoice!.SelectedValue = !visual.IsDark;
            _schemeChoice!.SelectedValue = visual.Scheme;
            var sizes = new[] { new VisualChoice<double>("Standard\n100%",1), new("125%",1.25),
                new("Large\n150%",1.5),new("175%",1.75),new("Extra large\n200%",2) };
            _textSizeChoice!.ItemsSource = sizes.Any(size => size.Value == _settings.InterfaceScale) ? sizes :
                sizes.Append(new VisualChoice<double>($"Current — {_settings.InterfaceScale:P0}", _settings.InterfaceScale)).ToArray();
            _textSizeChoice.SelectedValue = _settings.InterfaceScale;
            _spacingChoice!.SelectedValue = visual.Spacing; _complexityChoice!.SelectedValue = visual.Complexity;
            _motionChoice!.SelectedValue = visual.Motion; _focusChoice!.SelectedValue = visual.Focus; _highlightChoice!.SelectedValue = visual.Highlight;
            _oneControlChoice!.IsChecked = visual.OneControlAtATime;
            _customHighlight!.Text = visual.CustomHighlight; _customBackground!.Text = visual.BackgroundColour;
            _customSurface!.Text = visual.SurfaceColour; _customText!.Text = visual.TextColour; _customSecondary!.Text = visual.SecondaryTextColour;
            _customPaletteHost!.Visibility = visual.Scheme == DisplayScheme.Custom ? Visibility.Visible : Visibility.Collapsed;
            _customHighlightHost!.Visibility = visual.Highlight == VisualHighlight.Custom ? Visibility.Visible : Visibility.Collapsed;
            if (visual.Scheme == DisplayScheme.Custom) _moreVisualOptions!.IsExpanded = true;
        }
        finally { _syncingVisualAccess = false; }
    }
    private void ChangeIndividualVisualPreferences()
    {
        if (!CanChangeVisualAccess) return;
        var visual = _settings.EffectiveVisualAccess with
        {
            Spacing = _spacingChoice!.SelectedValue is VisualSpacing spacing ? spacing : _settings.EffectiveVisualAccess.Spacing,
            Complexity = _complexityChoice!.SelectedValue is VisualComplexity complexity ? complexity : _settings.EffectiveVisualAccess.Complexity,
            Motion = _motionChoice!.SelectedValue is VisualMotion motion ? motion : _settings.EffectiveVisualAccess.Motion,
            Focus = _focusChoice!.SelectedValue is VisualFocus focus ? focus : _settings.EffectiveVisualAccess.Focus,
            Highlight = _highlightChoice!.SelectedValue is VisualHighlight highlight ? highlight : _settings.EffectiveVisualAccess.Highlight,
            OneControlAtATime = _oneControlChoice!.IsChecked == true
        };
        _settings = _settings with
        {
            VisualAccess = visual,
            ReduceInterfaceMotion = visual.Motion != VisualMotion.Standard,
            InterfaceScale = _textSizeChoice!.SelectedValue is double scale ? scale : _settings.InterfaceScale
        };
        CommitVisualAccess("Display preference changed.");
    }
    private void ApplyCustomVisualColours()
    {
        if (!CanChangeVisualAccess) return;
        var fields = _settings.EffectiveVisualAccess.Scheme == DisplayScheme.Custom
            ? new[] { _customBackground!, _customSurface!, _customText!, _customSecondary! }.Concat(_settings.EffectiveVisualAccess.Highlight == VisualHighlight.Custom ? new[] { _customHighlight! } : []).ToArray()
            : new[] { _customHighlight! };
        foreach (var field in fields)
            if (!Regex.IsMatch(field.Text.Trim(), "^#[0-9a-fA-F]{6}$"))
            {
                _visualAccessNotice!.Text = "Use # followed by six colour digits, for example #FFC629."; field.Focus(); return;
            }
        var visual = (_settings.EffectiveVisualAccess with
        {
            CustomHighlight = _customHighlight!.Text.Trim(),
            BackgroundColour = _customBackground!.Text.Trim(),
            SurfaceColour = _customSurface!.Text.Trim(),
            TextColour = _customText!.Text.Trim(),
            SecondaryTextColour = _customSecondary!.Text.Trim()
        }).Validate();
        _settings = _settings with { VisualAccess = visual, LightTheme = !visual.IsDark };
        CommitVisualAccess("Your colours are applied. Text and focus keep a readable contrast.");
    }
    private void CommitVisualAccess(string notice)
    {
        _settings = _settings.Validate();
        bool loading = _loading; _loading = true;
        try
        {
            LightThemeCheck.IsChecked = _settings.LightTheme;
            ReduceMotionCheck.IsChecked = _settings.ReduceInterfaceMotion;
            SyncLegacyTextSize();
            PopulateVisualAccessOptions();
        }
        finally { _loading = loading; }
        _app.ApplyVisualAccess(_settings); UpdateAccessibleViewport(); UpdateSystemTextSizeNotice();
        _lastPresentationSettings = null; RefreshStatus(); UpdateSimpleControlViews();
        if (_visualAccessNotice != null) _visualAccessNotice.Text = notice +
            (_app.ThemeAdjustments.Length == 0 ? "" : " " + string.Join(" ", _app.ThemeAdjustments));
        SaveSettings();
    }
    private void RegisterSectionHeadings()
    {
        foreach (var text in LayoutLogicalElements(Pages).OfType<TextBlock>())
            if (text != PageHeading && text.FontWeight == FontWeights.SemiBold &&
                (_legacyTextSizes.TryGetValue(text, out double original) ? original : text.FontSize / InterfaceTextScale) >= 18)
                AccessibilitySupport.SectionHeading(text);
    }
    private void UpdateSystemTextSizeNotice()
    {
        if (_systemTextSizeNotice == null) return;
        _systemTextSizeNotice.Text = _app.WindowsTextScale > 1
            ? $"Windows text size sets a minimum of {_app.WindowsTextScale:P0}. Relay is using {InterfaceTextScale:P0}. Your Relay text-size choice is kept."
            : "";
        _systemTextSizeNotice.Visibility = _app.WindowsTextScale > 1 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SyncLegacyTextSize()
    {
        while (ZoomCombo.Items.Count > 5) ZoomCombo.Items.RemoveAt(5);
        double step = (_settings.InterfaceScale - 1) * 4;
        if (Math.Abs(step - Math.Round(step)) < .0001) ZoomCombo.SelectedIndex = (int)Math.Round(step);
        else
        {
            ZoomCombo.Items.Add(new ComboBoxItem { Content = $"{_settings.InterfaceScale:P0}", Tag = _settings.InterfaceScale });
            ZoomCombo.SelectedIndex = 5;
        }
    }
}

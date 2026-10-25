using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ControllerMouse.Core;
using MouseButton = ControllerMouse.Core.MouseButton;

namespace ControllerMouse.App;

internal sealed class SetupWindow : Window
{
    private readonly Settings _preferences;
    private readonly Func<SetupChoices, string?> _finish;
    private readonly Action<Settings> _preview;
    private readonly StackPanel _body = new() { Margin = new Thickness(26, 12, 26, 20) };
    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _heading = new() { FontWeight = FontWeights.SemiBold, Focusable = true, TextWrapping = TextWrapping.Wrap };
    private readonly Border _headingOuter = new() { Padding = new Thickness(2), CornerRadius = new CornerRadius(7) },
        _headingInner = new() { Padding = new Thickness(4), CornerRadius = new CornerRadius(4) };
    private readonly TextBlock _step = new(), _notice = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _back = new() { Content = "Back", MinWidth = 90, MinHeight = 48 };
    private readonly Button _next = new() { Content = "Next", MinWidth = 140, MinHeight = 48 };
    private readonly Button _practice = new() { Content = "Try selecting this button", MinHeight = 64, Margin = new Thickness(0, 8, 0, 16) };
    private readonly Expander _moreHelp = new() { Header = "More help", Margin = new Thickness(0, 12, 0, 12) };
    private readonly Expander _buttonGuide = new() { Header = "Button guide", Margin = new Thickness(0, 12, 0, 12) };
    private readonly Expander _otherControlsHelp = new() { Header = "Mouse and keyboard help", Margin = new Thickness(0, 12, 0, 12) };
    private readonly Dictionary<string, CheckBox> _choices = new();
    private readonly Dictionary<SetupInputMethod, Button> _methods = new();
    private readonly Dictionary<SetupInputMethod, TextBlock> _methodStates = new();
    private SetupInputMethod _inputMethod = SetupInputMethod.Controller;
    private Settings _previewSettings = new();
    private bool _magnifierBeforePedals, _oneButtonWithController = true;
    private int _page, _hits;
    internal bool Completed { get; private set; }
    internal SetupChoices Choices => new()
    {
        InputMethod = _inputMethod,
        SimpleControls = Selected("simple"),
        LargerText = Selected("text"),
        Magnifier = _inputMethod == SetupInputMethod.Pedals || Selected("magnifier"),
        ReadAloud = Selected("reader"),
        SteadyMovement = _inputMethod == SetupInputMethod.Controller && Selected("steady"),
        OneButton = _inputMethod != SetupInputMethod.Controller || Selected("one"),
        Keyboard = Selected("keyboard"),
        Captions = Selected("captions"),
        VoiceTyping = Selected("voice"),
        Quiet = _inputMethod != SetupInputMethod.Controller || Selected("quiet"),
        LearnMovement = _inputMethod == SetupInputMethod.Controller && Selected("learn")
    };
    private bool Selected(string id) => _choices[id].IsChecked == true;

    internal async System.Threading.Tasks.Task ExportDemoPages(string output)
    {
        for (int page = 0; page < 4; page++)
        {
            _page = page; RenderPage();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
            foreach (var choice in _choices.Values.Where(c => c.IsVisible))
                if (choice.Content is TextBlock label && (label.ActualWidth <= 0 || VisualTreeHelper.GetParent(label) == null))
                    throw new InvalidOperationException("A demo setup choice label was not rendered.");
            Capture(output, $"0{page + 2}-setup-{page + 1}.png");
        }
    }

    internal SetupWindow(Settings preferences, Func<SetupChoices, string?> finish, Action<Settings> preview, ControllerKind controllerKind = ControllerKind.Unknown)
    {
        // Controller identity does not reveal a person's accessories or input needs.
        _ = controllerKind;
        _preferences = preferences; _finish = finish; _preview = preview;
        Title = "Set up Relay"; Width = 760; Height = 780; MinWidth = 480; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");
        SetResourceReference(FontSizeProperty, "MainFontSize");
        SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
        _heading.SetResourceReference(TextBlock.FontSizeProperty, "PageTitleFontSize");
        _step.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        _notice.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        AccessibilitySupport.EnableKeyboardAccess(this);
        AccessibilitySupport.Heading(_heading);
        AutomationProperties.SetIsDialog(this, true);
        AutomationProperties.SetAutomationId(_heading, "SetupHeading");
        var page = new StackPanel();
        foreach (var (frame, thickness, brush) in new[] { (_headingOuter, "FocusOuterThickness", "FocusRingBrush"), (_headingInner, "FocusInnerThickness", "FocusInnerBrush") })
        {
            frame.SetResourceReference(Border.BorderThicknessProperty, thickness);
            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Transparent));
            var focused = new DataTrigger { Binding = new Binding(nameof(IsKeyboardFocused)) { Source = _heading }, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension(brush)));
            style.Triggers.Add(focused); frame.Style = style;
        }
        _headingInner.Child = _heading; _headingOuter.Child = _headingInner;
        var header = new StackPanel { Margin = new Thickness(26, 22, 26, 12) }; header.Children.Add(_step); header.Children.Add(_headingOuter); page.Children.Add(header);
        page.Children.Add(_body);
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.Focusable = false;
        var footer = new StackPanel { Margin = new Thickness(26, 12, 26, 22) }; footer.Children.Add(_notice);
        var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var later = new Button { Content = "Set up later", MinHeight = 48, Margin = new Thickness(0, 0, 12, 8) };
        AutomationProperties.SetAutomationId(later, "SetupLater");
        AutomationProperties.SetHelpText(later, "Close setup and keep your current profile.");
        later.Click += (_, _) => Close(); buttons.Children.Add(later);
        _back.Margin = new Thickness(0, 0, 12, 8); _next.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetAutomationId(_back, "SetupBack"); AutomationProperties.SetAutomationId(_next, "SetupNext");
        _next.Style = (Style)FindResource("Primary"); buttons.Children.Add(_back); buttons.Children.Add(_next); footer.Children.Add(buttons);
        page.Children.Add(footer); _scroll.Content = page; Content = _scroll;
        foreach (var (id, label) in new[] {
            ("text", "Make text and buttons bigger"), ("reader", "Add a Narrator shortcut"), ("magnifier", "Zoom in on the screen"),
            ("simple", "Show one controller input at a time"), ("keyboard", "Use the on-screen keyboard"),
            ("steady", "Make the pointer steadier"), ("one", "Use one button for each tool"),
            ("captions", "Show live captions (Windows 11)"), ("voice", "Speak to type (Windows + H)"), ("quiet", "Turn off controller vibration"),
            ("learn", "Learn how I move the pointer") })
        {
            var check = new CheckBox
            {
                Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                MinHeight = 48,
                IsChecked = id is "keyboard" or "text" or "simple" or "one"
            };
            AutomationProperties.SetName(check, label); _choices.Add(id, check);
            AutomationProperties.SetAutomationId(check, "Setup_" + id);
        }
        _choices["captions"].IsEnabled = Environment.OSVersion.Version.Build >= 22621;
        AutomationProperties.SetHelpText(_choices["captions"], "Requires Windows 11 version 22H2 or later. Windows manages language and audio setup.");
        _choices["text"].Checked += (_, _) => SetSize(true); _choices["text"].Unchecked += (_, _) => SetSize(false);
        AutomationProperties.SetHelpText(_choices["reader"], "Add a button to start or stop Windows Narrator. This choice does not turn Narrator on during setup.");
        AutomationProperties.SetHelpText(_choices["simple"], "Show one input and its assignment, with Previous and Next buttons. You can change this later in Accessibility.");
        AutomationProperties.SetHelpText(_choices["learn"], "Available when a stick controls the pointer. Switch-only navigation does not use movement learning.");
        AddMethod(SetupInputMethod.Controller, "Controller or joystick", "Needs an Xbox-compatible controller or joystick with a D-pad or stick.");
        AddMethod(SetupInputMethod.Switches, "Four switches for menus", "Needs four inputs assigned to Xbox A, B, X and Y.");
        AddMethod(SetupInputMethod.Pedals, "Switches and zoom pedals", "Needs A, B, X and Y switches, plus LT and RT zoom pedals.");
        var extras = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (string id in new[] { "captions", "voice", "quiet", "learn" }) extras.Children.Add(_choices[id]);
        _moreHelp.Content = extras;
        _otherControlsHelp.Content = new TextBlock { Text = "Use a mouse to select. With a keyboard: Tab goes next, Shift + Tab goes previous, Enter selects and Escape goes back.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        ((TextBlock)_otherControlsHelp.Content).SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        foreach (var (expander, id, help) in new[] {
            (_moreHelp, "SetupMoreHelp", "Optional captions, voice typing, vibration and pointer learning."),
            (_otherControlsHelp, "SetupOtherControlsHelp", "Optional mouse and physical keyboard instructions."),
            (_buttonGuide, "SetupButtonGuide", "Open the full list of assignments for your new profile.") })
        {
            AutomationProperties.SetAutomationId(expander, id); AutomationProperties.SetName(expander, expander.Header.ToString());
            AutomationProperties.SetHelpText(expander, help); expander.SetResourceReference(FontSizeProperty, "MainFontSize");
        }
        AutomationProperties.SetName(_practice, "Try selecting this button"); AutomationProperties.SetAutomationId(_practice, "SetupPractice");
        _practice.Click += (_, _) => { _hits++; _notice.Text = $"Selected {_hits} time{(_hits == 1 ? "" : "s")}. You can save when you are ready."; };
        AutomationProperties.SetHelpText(_next, "Continue to the next step. On the final step, save your choices as a new profile.");
        _next.IsDefault = true;
        _back.Click += (_, _) => GoBack();
        _next.Click += (_, _) => Advance();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None) return;
            if (_page > 0) GoBack(); else Close();
            e.Handled = true;
        };
        AccessibilitySupport.AnnounceChanges(_notice);
        Loaded += (_, _) => { Width = Math.Min(Width, SystemParameters.WorkArea.Width); Height = Math.Min(Height, SystemParameters.WorkArea.Height); AccessibilitySupport.FocusHeading(_heading); };
        RefreshMethodState(); RenderPage();
    }
    private void AddMethod(SetupInputMethod method, string name, string description)
    {
        var content = new StackPanel();
        var title = new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.FontSizeProperty, "SectionFontSize");
        var help = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        help.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        var state = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        state.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        content.Children.Add(title); content.Children.Add(help); content.Children.Add(state);
        var button = new Button
        {
            Content = content,
            MinHeight = 88,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12)
        };
        button.SetResourceReference(FontSizeProperty, "MainFontSize");
        AutomationProperties.SetName(button, name); AutomationProperties.SetHelpText(button, description);
        AutomationProperties.SetAutomationId(button, "SetupMethod_" + method);
        button.Click += (_, _) => ChooseMethod(method);
        _methods.Add(method, button); _methodStates.Add(method, state);
    }
    private void ChooseMethod(SetupInputMethod method)
    {
        if (_inputMethod == method) return;
        if (_inputMethod == SetupInputMethod.Pedals) _choices["magnifier"].IsChecked = _magnifierBeforePedals;
        if (_inputMethod == SetupInputMethod.Controller) _oneButtonWithController = Selected("one");
        _inputMethod = method;
        if (method == SetupInputMethod.Pedals) { _magnifierBeforePedals = Selected("magnifier"); _choices["magnifier"].IsChecked = true; }
        _choices["one"].IsChecked = method == SetupInputMethod.Controller ? _oneButtonWithController : true;
        RefreshMethodState();
        _notice.Text = MethodName(method) + " selected. Setup controls stay the same: A select, B next, X previous, Y back.";
    }
    private void RefreshMethodState()
    {
        foreach (var (method, button) in _methods)
        {
            bool selected = method == _inputMethod;
            _methodStates[method].Text = selected ? "Selected" : "Choose this";
            AutomationProperties.SetItemStatus(button, selected ? "Selected controls" : "Available controls");
            button.SetResourceReference(BackgroundProperty, selected ? "SelectionSurfaceBrush" : "SurfaceRaisedBrush");
            button.SetResourceReference(ForegroundProperty, selected ? "SelectionTextBrush" : "TextBrush");
        }
        bool pointer = _inputMethod == SetupInputMethod.Controller;
        _choices["steady"].IsEnabled = pointer; _choices["learn"].IsEnabled = pointer; _choices["one"].IsEnabled = pointer;
        _choices["quiet"].Visibility = pointer ? Visibility.Visible : Visibility.Collapsed;
        _choices["magnifier"].IsEnabled = _inputMethod != SetupInputMethod.Pedals;
        AutomationProperties.SetHelpText(_choices["magnifier"], _inputMethod == SetupInputMethod.Pedals
            ? "Zoom is included with this pedal layout. LT zooms out and RT zooms in after saving."
            : "Add Magnifier zoom controls to your profile.");
    }
    private void SetSize(bool large)
    {
        foreach (var choice in _choices.Values) choice.SetResourceReference(FontSizeProperty, large ? "SectionFontSize" : "MainFontSize");
        foreach (var text in _body.Children.OfType<TextBlock>()) text.SetResourceReference(TextBlock.FontSizeProperty, large ? "SectionFontSize" : "MainFontSize");
    }
    private TextBlock Label(string text, bool bold = false)
    {
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        label.SetResourceReference(TextBlock.FontSizeProperty, Selected("text") ? "SectionFontSize" : "MainFontSize");
        return label;
    }
    private void Text(string text, bool bold = false) => _body.Children.Add(Label(text, bold));
    private string MenuGuide => _inputMethod == SetupInputMethod.Controller
        ? "D-pad moves. RB next. LB previous. A selects. B goes back."
        : "A selects. B goes next. X goes previous. Y goes back.";
    private void RenderPage()
    {
        _body.Children.Clear(); _notice.Text = "";
        _step.Text = $"Step {_page + 1} of 4";
        _heading.Text = _page switch { 0 => "Choose your controls", 1 => "Help with reading", 2 => "Typing and movement", _ => "Ready to start" };
        AutomationProperties.SetName(_heading, $"{_step.Text}. {_heading.Text}");
        _back.IsEnabled = _page > 0; _next.Content = _page == 3 ? "Save and start" : "Next";
        if (_page == 0)
        {
            Text("Choose how you want to use Relay.");
            Text("During setup: A selects. B next. X previous. Y back.");
            foreach (var button in _methods.Values) _body.Children.Add(button);
            Text("Connect and assign your accessories first. Relay receives Xbox buttons; it cannot tell which accessory you plugged in.");
            _body.Children.Add(_otherControlsHelp);
        }
        else if (_page == 1)
        {
            Text("Choose any help you want. You can change it later.");
            foreach (string id in new[] { "text", "reader", "magnifier", "simple" }) _body.Children.Add(_choices[id]);
            if (_inputMethod == SetupInputMethod.Pedals) Text("Zoom is included. After saving: LT zooms out, RT zooms in.");
        }
        else if (_page == 2)
        {
            Text("Choose what makes typing and control easier.");
            _body.Children.Add(_choices["keyboard"]);
            if (_inputMethod == SetupInputMethod.Controller) _body.Children.Add(_choices["steady"]);
            _body.Children.Add(_choices["one"]);
            if (_inputMethod != SetupInputMethod.Controller) Text("Each switch does one thing. Vibration is off. Pointer learning needs a stick.");
            _body.Children.Add(_moreHelp);
        }
        else
        {
            Text("After saving: " + MethodName(_inputMethod) + ". " + MenuGuide, true);
            if (_inputMethod == SetupInputMethod.Controller) Text("To use the pointer, move the left stick. A clicks. The right stick scrolls.");
            else if (_inputMethod == SetupInputMethod.Pedals) Text("After saving: LT zooms out, RT zooms in.");
            Text("Your help: " + NeedsSummary() + ".");
            Text("Optional practice: B goes next, X goes previous. Select the button below with A or Enter.");
            _body.Children.Add(_practice);
            var guide = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var settings = SetupPlan.Build(Choices, _preferences);
            foreach (var option in ControlMapping.Controls)
                guide.Children.Add(Label(PlainControl(option.Control) + ": " + PlainBinding(ControlMapping.Resolve(settings, option.Control))));
            foreach (var chord in settings.ButtonChords ?? [])
                guide.Children.Add(Label(PlainControl(chord.First) + " + " + PlainControl(chord.Second) + ": " + PlainBinding(chord.Action)));
            guide.Children.Add(Label("View + Menu pauses Relay. Other tools are on Relay’s Accessibility page."));
            _buttonGuide.Content = guide; _body.Children.Add(_buttonGuide);
            Text("Save and start keeps this as My setup. Back lets you change a choice.");
        }
        // Tool mappings and learning become active only after the single final save.
        _previewSettings = SetupPlan.Navigation(_preferences, SetupInputMethod.Switches);
        _preview(_previewSettings);
        SetSize(Selected("text"));
        _scroll.ScrollToHome(); AccessibilitySupport.FocusHeading(_heading);
    }
    private static string MethodName(SetupInputMethod method) => method switch
    {
        SetupInputMethod.Switches => "Four switches for menus",
        SetupInputMethod.Pedals => "Switches and zoom pedals",
        _ => "Controller or joystick"
    };
    private string NeedsSummary()
    {
        var needs = new List<string>();
        if (Choices.LargerText) needs.Add("bigger text"); if (Choices.ReadAloud) needs.Add("Narrator shortcut");
        if (Choices.Magnifier) needs.Add("zoom"); if (Choices.SimpleControls == true) needs.Add("one input at a time");
        if (Choices.Keyboard) needs.Add("on-screen keyboard"); if (Choices.SteadyMovement) needs.Add("steadier pointer");
        if (Choices.Captions) needs.Add("captions"); if (Choices.VoiceTyping) needs.Add("voice typing");
        if (Choices.Quiet) needs.Add("no vibration"); if (Choices.LearnMovement) needs.Add("pointer learning");
        return needs.Count == 0 ? "standard controls" : string.Join(", ", needs);
    }
    private void GoBack() { if (_page > 0) { _page--; RenderPage(); } }
    private void Advance()
    {
        if (_page < 3) { _page++; RenderPage(); return; }
        string? error = _finish(Choices);
        if (error != null) { _notice.Text = error; _notice.BringIntoView(); return; }
        Completed = true; Close();
    }
    private static string PlainControl(ControllerMouse.Core.Control control) => control switch
    {
        ControllerMouse.Core.Control.LeftStick => "Press the left stick",
        ControllerMouse.Core.Control.RightStick => "Press the right stick",
        ControllerMouse.Core.Control.LeftShoulder => "LB (left top button)",
        ControllerMouse.Core.Control.RightShoulder => "RB (right top button)",
        ControllerMouse.Core.Control.LeftTrigger => "LT (left trigger)",
        ControllerMouse.Core.Control.RightTrigger => "RT (right trigger)",
        _ => ControlMapping.Controls.First(c => c.Control == control).Name
    };
    private static string PlainBinding(ControlBinding binding) => binding.Kind switch
    {
        BindingKind.None => "not assigned",
        BindingKind.Mouse => binding.Mouse switch { MouseButton.Right => "right click", MouseButton.Middle => "middle click", _ => "left click" },
        BindingKind.Precision => "move the pointer slowly",
        BindingKind.Boost => "move the pointer faster",
        BindingKind.Pointer => "move the pointer " + (binding.Key switch { 38 => "up", 40 => "down", 37 => "left", _ => "right" }),
        BindingKind.Keyboard => binding.Key switch
        {
            13 => "select (Enter)",
            9 when binding.Modifiers == 4 => "previous control (Shift + Tab)",
            9 => "next control (Tab)",
            27 => "go back (Escape)",
            38 => "up arrow",
            40 => "down arrow",
            37 => "left arrow",
            39 => "right arrow",
            _ => "keyboard key " + binding.Key
        },
        _ => PlainAction(binding.Shortcut)
    };
    private static string PlainAction(Shortcut action) => action switch
    {
        Shortcut.None => "not assigned",
        Shortcut.OnScreenKeyboard => "open the keyboard",
        Shortcut.VoiceTyping => "speak to type (Windows + H)",
        Shortcut.AccessibilityHub => "open the accessibility tools",
        Shortcut.Narrator => "turn screen reading on or off",
        Shortcut.MagnifierStart => "start zooming",
        Shortcut.ZoomIn => "zoom in",
        Shortcut.ZoomOut => "zoom out",
        Shortcut.CloseMagnifier => "stop zooming",
        Shortcut.MagnifierView => "change the zoom view",
        Shortcut.LiveCaptions => "turn captions on or off",
        _ => AccessibilityCatalog.Actions.FirstOrDefault(a => a.Action == action)?.Name ?? action.ToString()
    };
    internal void VerifyPages(string output)
    {
        if (_inputMethod != SetupInputMethod.Controller || !Choices.OneButton || Choices.SimpleControls != true || _methods.Count != 3)
            throw new InvalidOperationException("Setup did not start with explicit controller controls and independent simple one-button defaults.");
        VerifyFixedPreview();
        Invoke(_methods[SetupInputMethod.Switches]);
        if (Choices.InputMethod != SetupInputMethod.Switches || _choices["learn"].IsEnabled || _choices["steady"].IsEnabled || Choices.LearnMovement || Choices.SteadyMovement)
            throw new InvalidOperationException("Switch setup retained pointer-only assistance or inferred the wrong input method.");
        VerifyFixedPreview();
        _choices["magnifier"].IsChecked = false;
        Invoke(_methods[SetupInputMethod.Pedals]);
        if (!Choices.Magnifier || _choices["magnifier"].IsEnabled || _methods.Any(pair =>
            AutomationProperties.GetItemStatus(pair.Value) != (pair.Key == SetupInputMethod.Pedals ? "Selected controls" : "Available controls")))
            throw new InvalidOperationException("Pedal setup lacks its mandatory zoom choice or readable current-control state.");
        VerifyFixedPreview();
        Invoke(_methods[SetupInputMethod.Controller]);
        if (Choices.Magnifier || !_choices["magnifier"].IsEnabled || !_choices["learn"].IsEnabled || !Choices.OneButton)
            throw new InvalidOperationException("Returning to controller controls did not restore independent reading and movement choices.");
        _choices["magnifier"].IsChecked = true; _choices["steady"].IsChecked = true; _choices["learn"].IsChecked = true;
        for (int page = 0; page < 4; page++)
        {
            _page = page; RenderPage(); UpdateLayout();
            VerifyAccessibleControls(this);
            VerifyFixedPreview();
            if (!_heading.IsKeyboardFocused || UIElementAutomationPeer.CreatePeerForElement(_heading)?.GetName() != $"Step {page + 1} of 4. {_heading.Text}")
                throw new InvalidOperationException("Setup step did not focus its readable, correctly numbered heading.");
            if (page == 2 && (_moreHelp.IsExpanded || new[] { "captions", "voice", "quiet", "learn" }.Any(id => _choices[id].IsVisible)))
                throw new InvalidOperationException("Optional setup choices were exposed before More help was expanded.");
            if (page == 3 && (_buttonGuide.IsExpanded || _buttonGuide.Content is not StackPanel guide || guide.Children.Count < ControlMapping.Controls.Count))
                throw new InvalidOperationException("Setup's full actual assignment guide is missing or was expanded automatically.");
            Capture(output, $"setup-{page + 1}.png");
        }
        _heading.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_heading)!, Environment.TickCount, Key.Escape)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        if (_page != 2 || !_heading.IsKeyboardFocused || !Choices.Magnifier || !Choices.LearnMovement || Choices.InputMethod != SetupInputMethod.Controller)
            throw new InvalidOperationException("Escape did not return to the previous setup step and preserve choices.");
        Expand(_moreHelp); VerifyAccessibleControls(this);
        if (!_choices["learn"].IsVisible || !_choices["quiet"].IsVisible) throw new InvalidOperationException("More help did not expose its real accessible options.");
        _moreHelp.IsExpanded = false;
        VerifyTypography(output);
        _page = 3; RenderPage(); Expand(_buttonGuide); VerifyAccessibleControls(this); _buttonGuide.IsExpanded = false;
        int hits = _hits; Invoke(_practice);
        if (_hits != hits + 1 || !_notice.Text.StartsWith("Selected ", StringComparison.Ordinal) || Completed)
            throw new InvalidOperationException("Optional setup practice did not acknowledge one real selection without saving.");
        _choices["one"].IsChecked = false;
        if (SetupPlan.Build(Choices, _preferences).ButtonChords?.Length != 2)
            throw new InvalidOperationException("Explicit combination shortcuts were not available when the one-button default was turned off.");
        _choices["one"].IsChecked = true;
        _page = 3; RenderPage();
        Advance();
    }
    private void VerifyFixedPreview()
    {
        foreach (var (control, key, modifiers) in new[] {
            (ControllerMouse.Core.Control.A, 13, 0), (ControllerMouse.Core.Control.B, 9, 0),
            (ControllerMouse.Core.Control.X, 9, 4), (ControllerMouse.Core.Control.Y, 27, 0) })
        {
            var binding = ControlMapping.Resolve(_previewSettings, control);
            if (binding.Kind != BindingKind.Keyboard || binding.Key != key || binding.Modifiers != modifiers)
                throw new InvalidOperationException("Setup preview changed its temporary A/B/X/Y menu layout.");
        }
        if (_previewSettings.MotionLearning != LearningMode.Off || ControlMapping.Controls.Any(option =>
            ControlMapping.Resolve(_previewSettings, option.Control) is { Kind: BindingKind.Shortcut, Shortcut: not (Shortcut.Up or Shortcut.Down or Shortcut.Left or Shortcut.Right or Shortcut.None) }))
            throw new InvalidOperationException("Setup preview could learn movement or open a Windows tool before saving.");
    }
    private void VerifyTypography(string output)
    {
        string[] keys = ["MainFontSize", "CaptionFontSize", "PageTitleFontSize", "SectionFontSize", "ControlMinHeight"];
        var originals = keys.ToDictionary(key => key, key => Resources.Contains(key) ? Resources[key] : null);
        double originalWidth = Width, originalHeight = Height;
        try
        {
            Width = 480; Height = 480;
            foreach (double scale in new[] { 1d, 2d })
            {
                foreach (var (key, size) in new[] { ("MainFontSize", 16d), ("CaptionFontSize", 14d), ("PageTitleFontSize", 32d), ("SectionFontSize", 20d), ("ControlMinHeight", 44d) })
                    Resources[key] = size * scale;
                for (int page = 0; page < 4; page++)
                {
                    _page = page; RenderPage(); UpdateLayout();
                    if (_heading.FontSize != 32 * scale || _step.FontSize != 14 * scale || _notice.FontSize != 14 * scale ||
                        _choices.Values.Where(choice => choice.IsVisible).Any(choice => choice.FontSize != 20 * scale))
                        throw new InvalidOperationException("Setup's text does not follow the global scaled font resources.");
                    if (!_heading.IsKeyboardFocused || !Equals(_headingOuter.BorderBrush, TryFindResource("FocusRingBrush")) ||
                        !Equals(_headingInner.BorderBrush, TryFindResource("FocusInnerBrush")) || _headingOuter.BorderThickness.Left < 3 || _headingInner.BorderThickness.Left < 1)
                        throw new InvalidOperationException("Setup's actual heading focus lacks its visible, theme-aware double outline.");
                    var targets = new List<FrameworkElement> { _headingOuter, _next };
                    if (page == 0) targets.AddRange(_methods.Values);
                    if (page == 1) targets.AddRange(new[] { "text", "reader", "magnifier", "simple" }.Select(id => _choices[id]));
                    if (page == 2)
                    {
                        Expand(_moreHelp); targets.AddRange(new[] { "keyboard", "steady", "one", "captions", "voice", "quiet", "learn" }.Select(id => _choices[id]));
                    }
                    if (page == 3) { targets.Add(_practice); Expand(_buttonGuide); }
                    foreach (var action in targets)
                    {
                        action.BringIntoView(); UpdateLayout();
                        if (action is CheckBox check && check.FontSize != 20 * scale)
                            throw new InvalidOperationException("An expanded setup choice does not follow its scaled font resource.");
                        var bounds = action.TransformToAncestor(_scroll).TransformBounds(new Rect(action.RenderSize));
                        if (action.ActualHeight < 44 || bounds.Top < -1 || bounds.Bottom > _scroll.ActualHeight + 1 ||
                            bounds.Left < -1 || bounds.Right > _scroll.ViewportWidth + 1)
                            throw new InvalidOperationException($"Setup page {page + 1} target {AutomationProperties.GetAutomationId(action)} cannot be shown at {scale * 100}% inside its single 480px scroll. Bounds {bounds}; viewport {_scroll.ViewportWidth}×{_scroll.ActualHeight}.");
                    }
                    VerifyAccessibleControls(this);
                    _moreHelp.IsExpanded = false; _buttonGuide.IsExpanded = false;
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, "setup-typography.txt"),
                "PASS: all four short setup pages at 480×480 follow 100% and 200% global resources; focused headings have both theme-aware outline layers. All three control choices, reading choices, optional movement/typing choices, practice and final save have readable real actions and can be brought fully into the single scroll with at least 44px targets. Optional sections stay collapsed until requested. No desktop input or Narrator listening is claimed.");
        }
        finally
        {
            foreach (var key in keys) { if (originals[key] is { } value) Resources[key] = value; else Resources.Remove(key); }
            Width = originalWidth; Height = originalHeight; _moreHelp.IsExpanded = false; _buttonGuide.IsExpanded = false;
            RenderPage(); UpdateLayout();
        }
    }
    private void Invoke(Button button)
    {
        var invoke = UIElementAutomationPeer.CreatePeerForElement(button)?.GetPattern(PatternInterface.Invoke) as IInvokeProvider
            ?? throw new InvalidOperationException("Setup option has no real Invoke pattern.");
        invoke.Invoke(); DrainInvocations();
    }
    private void Expand(Expander expander)
    {
        var pattern = UIElementAutomationPeer.CreatePeerForElement(expander)?.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider
            ?? throw new InvalidOperationException("Setup optional section has no real ExpandCollapse pattern.");
        pattern.Expand(); DrainInvocations();
    }
    private void DrainInvocations()
    {
        var frame = new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); UpdateLayout();
    }
    private void Capture(string output, string name)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        using var file = System.IO.File.Create(System.IO.Path.Combine(output, name));
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
    private static void VerifyAccessibleControls(DependencyObject parent)
    {
        if (parent is System.Windows.Controls.Control control && control.IsEnabled && control.IsVisible && control is Button or CheckBox or Expander)
        {
            var pattern = control is CheckBox ? PatternInterface.Toggle : control is Expander ? PatternInterface.ExpandCollapse : PatternInterface.Invoke;
            var peer = UIElementAutomationPeer.CreatePeerForElement(control);
            if (peer == null || string.IsNullOrWhiteSpace(peer.GetName()) ||
                peer.GetPattern(pattern) == null)
                throw new InvalidOperationException("Visible setup control is missing its accessible name or action.");
        }
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>()) VerifyAccessibleControls(child);
    }
    internal void VerifyFailedSave()
    {
        _page = 3; RenderPage(); Advance();
        if (Completed || !IsVisible || string.IsNullOrWhiteSpace(_notice.Text)) throw new InvalidOperationException("Setup save failure was hidden.");
        Close();
    }
}

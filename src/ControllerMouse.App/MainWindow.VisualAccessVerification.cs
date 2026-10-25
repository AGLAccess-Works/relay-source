using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private async Task RunVisualAccessVerification(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Visual Access verification requires the isolated preview without desktop output.");
        var originalSettings = _settings;
        var originalProfiles = _app.Profiles;
        var originalStatus = _lastPresentationStatus;
        string originalSelection = _selectedInput;
        string? originalLastInput = _lastInputId;
        string[] originalActiveInputs = _previousActiveInputs.ToArray();
        int originalPage = Pages.SelectedIndex;
        bool originalLoading = _loading, originalPorts = _portsView, originalDetailsExpanded = HardwareInputDetails.IsExpanded;
        bool refreshRunning = _refresh.IsEnabled, saveRunning = _save.IsEnabled;
        double originalWidth = Width, originalHeight = Height;
        var originalWindowState = WindowState;
        var report = new StringBuilder();
        _refresh.Stop(); _save.Stop();
        var preview = new ControllerStatus(true, true, true, 0, "Preview", default, null, false);
        Task Drain() => Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle).Task;
        static void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Visual Access: " + detail);
        }
        static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element)
            ?? throw new InvalidOperationException("Visual Access element has no automation peer.");
        static Color Colour(object? value) => value is SolidColorBrush brush ? brush.Color :
            throw new InvalidOperationException("Visual Access colour is not a solid brush.");
        async Task ChooseProfile(SettingChoiceButton? choice, int index)
        {
            await Choose(choice, index);
        }
        async Task Choose(SettingChoiceButton? choice, int index)
        {
            var actualChoice = choice ?? throw new InvalidOperationException("Visual Access setting button is missing.");
            if (!actualChoice.IsVisible) _moreVisualOptions!.IsExpanded = true;
            actualChoice.BringIntoView(); await Drain();
            Check(actualChoice.Focus(), "the display setting cannot receive keyboard focus.");
            var opened = new TaskCompletionSource<SettingChoiceWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (actualChoice.ActiveChoiceWindow is { } window) opened.TrySetResult(window);
                else opened.TrySetException(new InvalidOperationException("The display choice window did not open."));
            }));
            var invoke = Peer(actualChoice).GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            Check(invoke != null, "the display setting has no native Invoke pattern.");
            invoke!.Invoke();
            var window = await opened.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Drain();
            Check(index >= 0 && index < window.ChoiceButtons.Count && !string.IsNullOrWhiteSpace(Peer(window.ChoiceButtons[index]).GetName()),
                "the display choice is missing its actual named button.");
            ((IInvokeProvider)Peer(window.ChoiceButtons[index]).GetPattern(PatternInterface.Invoke)!).Invoke();
            await Drain();
            Check(!window.IsVisible && actualChoice.SelectedIndex == index, "choosing an option did not close/commit the display preference.");
        }
        async Task ChooseDirect(DirectSettingChoiceRow? choice, int index)
        {
            var row = choice ?? throw new InvalidOperationException("Direct preference row is missing.");
            if (!row.IsVisible) _moreVisualOptions!.IsExpanded = true;
            Check(index >= 0 && index < row.ChoiceButtons.Count, "direct option index is unavailable.");
            var button = row.ChoiceButtons[index]; button.BringIntoView(); await Drain();
            Check(button.Focus() && !string.IsNullOrWhiteSpace(Peer(button).GetName()), "direct option has no focus or name.");
            var invoke = Peer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            Check(invoke != null, "direct option has no native Invoke pattern.");
            invoke!.Invoke(); await Drain();
            Check(row.SelectedIndex == index && Peer(button).GetItemStatus() == "Selected",
                "direct option did not commit its value and accessible selected status.");
        }
        async Task ToggleOneControl(bool enabled)
        {
            var choice = _oneControlChoice ?? throw new InvalidOperationException("Visual Access one-control option is missing.");
            if (choice.IsChecked == enabled) return;
            var toggle = Peer(choice).GetPattern(PatternInterface.Toggle) as IToggleProvider;
            Check(toggle != null, "one-control option has no native Toggle pattern.");
            toggle!.Toggle(); await Drain();
            Check(choice.IsChecked == enabled && _settings.EffectiveVisualAccess.OneControlAtATime == enabled,
                "the actual one-control Toggle did not commit its preference.");
        }
        async Task Invoke(Button button)
        {
            button.BringIntoView(); await Drain();
            var peer = Peer(button);
            Check(button.ActualWidth >= 44 && button.ActualHeight >= 44 &&
                !string.IsNullOrWhiteSpace(peer.GetName()) && !string.IsNullOrWhiteSpace(peer.GetAutomationId()),
                "a simplified input action lacks its name, ID or 44-pixel target.");
            var invoke = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            Check(invoke != null && button.Focus(), "a simplified input action cannot be invoked/focused.");
            invoke!.Invoke(); await Drain();
        }
        void ShowPreview(Gamepad pad = default)
        {
            _lastPresentationSettings = null;
            RefreshStatus(preview with { State = pad }); UpdateLayout();
        }
        static byte[] Pixels(FrameworkElement element)
        {
            element.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
            var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0); return pixels;
        }
        try
        {
            _loading = false; WindowState = WindowState.Normal; Width = 1440; Height = 900;
            _settings = originalSettings.WithVisualAccessScheme(DisplayScheme.RelayDefault) with
            {
                InterfaceScale = 1,
                ControllerType = ControllerPreference.Standard,
                AdaptiveAccessories = null
            };
            PopulateControls(); LoadBinding(); ReadSettings(); ShowPreview(); SelectPresentedInput("A");
            File.WriteAllText(Path.Combine(output, "visual-access-palette-contrast.txt"), VisualAccessTheme.VerifyPaletteContrast());
            report.AppendLine("PASS 489 resolved palette combinations: all seven schemes/highlight choices, hostile custom colours, and 216 custom highlights in each default appearance; readable text/actions, two-part focus on ordinary and filled controls, selection and artwork roles.");

            foreach (var scheme in Enum.GetValues<DisplayScheme>())
            {
                SelectPage(3); await Choose(_schemeChoice, (int)scheme); ShowPreview(); SelectPage(0); ShowPreview(); await Drain();
                Check(_settings.EffectiveVisualAccess.Scheme == scheme && _app.CurrentVisualAccess.Scheme == scheme,
                    "scheme selection did not reach the model and application resources: " + scheme);
                Check(_app.LightTheme == !_settings.EffectiveVisualAccess.IsDark, "legacy light/dark state disagrees with the chosen scheme.");
                foreach (var logoPath in new[] { BrandLogoTop, BrandLogoLeft, BrandLogoBase })
                {
                    Check(Colour(logoPath.Fill) == Colour(TryFindResource("BrandBrush")),
                        "the actual logo did not update to the current theme: " + scheme);
                    Check(VisualAccessTheme.Contrast(Colour(logoPath.Fill), Colour(Background)) >= 3,
                        "the actual logo disappears against the theme background: " + scheme);
                }
                if (scheme == DisplayScheme.CviSimplified)
                    Check(_settings.InterfaceScale >= 1.5 && ControlSimpleHost.IsVisible && !LiveSurface.IsVisible,
                        "the CVI starting point did not enlarge text and simplify the input view.");
                var action = LayoutLogicalElements(ControlInspectorHost).OfType<Button>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "ControlChangeAction");
                action.BringIntoView(); Check(action.Focus(), "the actual primary action cannot receive keyboard focus.");
                await Drain(); action.ApplyTemplate();
                var outer = action.Template.FindName("FocusOutline", action) as Border ?? throw new InvalidOperationException("Visual Access outer focus edge is missing.");
                var inner = action.Template.FindName("FocusInner", action) as Border ?? throw new InvalidOperationException("Visual Access inner focus edge is missing.");
                Check(outer.BorderThickness.Left >= 3 && inner.BorderThickness.Left >= 1,
                    "the actual action template does not retain both focus layers.");
                Check(Colour(outer.BorderBrush) == Colour(TryFindResource("FocusRingBrush")) &&
                    Colour(inner.BorderBrush) == Colour(TryFindResource("ActionFocusInnerBrush")),
                    "keyboard focus does not render the current scheme's two independent layers.");
                if (!_app.ContrastActive)
                {
                    Check(Colour(Background) == Colour(TryFindResource("SurfaceBackgroundBrush")), "the window did not use the current background role.");
                    Check(VisualAccessTheme.Contrast(Colour(action.Foreground), Colour(action.Background)) >= 4.5,
                        "the real primary action text has insufficient contrast.");
                    foreach (var label in LayoutVisualElements(action).OfType<TextBlock>().Where(label => label.IsVisible))
                        Check(VisualAccessTheme.Contrast(Colour(label.Foreground), Colour(action.Background)) >= 4.5,
                            "a primary action's child label overrides the readable foreground.");
                    Check(VisualAccessTheme.Contrast(Colour(inner.BorderBrush), Colour(outer.BorderBrush)) >= 3,
                        "the real inner/outer focus edges do not contrast.");
                }
                Check(action.ActualWidth >= 44 && action.ActualHeight >= 44 && Peer(action).GetPattern(PatternInterface.Invoke) != null,
                    "the real primary action lost its target size or Invoke pattern.");
                Check(new Rect(action.RenderSize).Contains(LayoutBounds(outer, action)) &&
                    new Rect(action.RenderSize).Contains(LayoutBounds(inner, action)),
                    "the primary action's focus outline extends beyond its scrollable target and can be clipped.");
                report.AppendLine("PASS " + scheme + ": native UIA scheme selection, applied preference/resources, actual named primary action, full target and both rendered focus edges.");
                CaptureRelay(output, "visual-access-" + scheme.ToString().ToLowerInvariant());
            }

            SelectPage(3); await Choose(_schemeChoice, (int)DisplayScheme.MaximumContrastDark);
            await ChooseDirect(_spacingChoice, (int)VisualSpacing.Wide); await ChooseDirect(_complexityChoice, (int)VisualComplexity.Reduced);
            await ChooseDirect(_motionChoice, (int)VisualMotion.Off); await ChooseDirect(_focusChoice, (int)VisualFocus.VeryStrong);
            await Choose(_highlightChoice, (int)VisualHighlight.Blue); await ChooseDirect(_textSizeChoice, 2); await ToggleOneControl(true);
            var independent = _settings.EffectiveVisualAccess;
            Check(independent.Scheme == DisplayScheme.MaximumContrastDark && independent.Spacing == VisualSpacing.Wide &&
                independent.Complexity == VisualComplexity.Reduced && independent.Motion == VisualMotion.Off &&
                independent.Focus == VisualFocus.VeryStrong && independent.Highlight == VisualHighlight.Blue && independent.OneControlAtATime &&
                _settings.InterfaceScale == 1.5, "changing one preference reset another independent preference.");
            report.AppendLine("PASS independent native preference selectors/Toggle: scheme, spacing, amount shown, motion, focus strength, highlight, text size and one-control mode coexist.");

            Check(Peer(ThemeButton).GetName() == "Use light theme" && Peer(ThemeButton).GetItemStatus() == "Current theme: Dark",
                "the quick theme action does not describe its direction and current theme.");
            await Invoke(ThemeButton);
            Check((_settings.EffectiveVisualAccess with { Scheme = independent.Scheme }) == independent && _settings.InterfaceScale == 1.5,
                "the quick light/dark action reset an independent reading preference.");
            Check(!_settings.EffectiveVisualAccess.IsDark && Peer(ThemeButton).GetName() == "Use dark theme" &&
                Peer(ThemeButton).GetItemStatus() == "Current theme: Light", "the actual theme action did not update its name and status after switching.");
            await Invoke(ThemeButton);
            Check(_settings.EffectiveVisualAccess == independent && _settings.InterfaceScale == 1.5,
                "the actual theme action did not restore the original scheme and reading preferences.");
            _settings = _settings with { VisualAccess = independent, LightTheme = !independent.IsDark }; PopulateControls(); ReadSettings();
            report.AppendLine("PASS the quick light/dark action changes colour scheme while preserving spacing, simplicity, motion, focus, highlight, text size and one-control preferences.");

            _settings = _settings with { InterfaceScale = 1.8 }; PopulateControls(); ReadSettings();
            Check(_settings.InterfaceScale == 1.8, "populating display controls rounded an imported non-preset text size.");
            var savedInterface = _settings;
            _app.Profiles = new ProfileLibrary(1, "visual-a", [
                new("visual-a","First display fixture",originalSettings with { PointerSpeed=650 }),
                new("visual-b","Second display fixture",originalSettings.WithVisualAccessScheme(DisplayScheme.SoftLight) with { PointerSpeed=1450,InterfaceScale=1 })
            ])
            { SetupCompleted = true, AccessibilityRevision = 1 };
            _settings = _app.Profiles.Active.Settings.WithInterfacePreferences(savedInterface);
            PopulateControls(); PopulateProfiles(); LoadBinding(); ReadSettings();
            await ChooseProfile(ProfileCombo, 1);
            Check(_app.Profiles.ActiveId == "visual-b" && _settings.PointerSpeed == 1450 &&
                _settings.EffectiveVisualAccess == independent && _settings.InterfaceScale == 1.8,
                "a real profile selection overwrote independent interface preferences or did not load its pointer setting.");
            await ChooseProfile(ProfileCombo, 0);
            Check(_app.Profiles.ActiveId == "visual-a" && _settings.PointerSpeed == 650 && _settings.EffectiveVisualAccess == independent && _settings.InterfaceScale == 1.8,
                "returning to a profile lost its controller setting or global display preferences.");
            report.AppendLine("PASS explicit profile choice Invoke round trip loads different pointer settings while retaining the entire independent display preference record and exact imported 180% text size.");

            var footerPreferences = _settings;
            string? previousSaveError = _saveError;
            try
            {
                Width = 640; Height = 480;
                _settings = _settings with { InterfaceScale = 2 };
                PopulateControls(); ReadSettings(); ShowPreview(); await Drain();
                Check(FooterPanel.Visibility == Visibility.Collapsed && AccessViewport.ActualHeight >= 144,
                    "a routine footer still displaces the compact large-text page.");
                _saveError = "Could not save settings. Try again.";
                ShowPreview(); await Drain();
                Check(FooterPanel.IsVisible && Footer.IsVisible && Footer.Text == _saveError,
                    "compact mode hid the actual save-failure feedback.");
                Check(AutomationProperties.GetLiveSetting(Footer) == AutomationLiveSetting.Polite && Peer(Footer).GetName() == _saveError,
                    "the revealed save warning lost its readable live-region information.");
                CheckCriticalText(Footer, "compact save warning");
                CaptureRelay(output, "compact-save-warning-200");
            }
            finally
            {
                _saveError = previousSaveError; _settings = footerPreferences;
                Width = 1440; Height = 900;
                PopulateControls(); ReadSettings(); ShowPreview(); await Drain();
            }
            report.AppendLine("PASS compact 200% view hides only the routine footer; an actual save warning remains visible and readable without changing text or input preferences.");

            SelectPage(3); await Choose(_schemeChoice, (int)DisplayScheme.RelayDefault); await ChooseDirect(_textSizeChoice, 0);
            await ToggleOneControl(true); _settings = _settings with { ControllerType = ControllerPreference.Standard };
            PopulateControls(); ReadSettings();
            foreach (var (page, prefix) in new[] { (0, "Control"), (6, "Mapping"), (9, "Hardware") })
            {
                SelectPage(page); ShowPreview(); await Drain();
                var view = _simpleInputViews.Single(candidate => candidate.Prefix == prefix);
                Check(view.Host.IsVisible && !view.Surface.IsVisible && _settings.EffectiveVisualAccess.Complexity == VisualComplexity.Standard,
                    "independent one-control mode did not replace the drawing on " + prefix);
                var order = view.Picker.Items.Cast<VisualChoice<string>>().Select(choice => choice.Value).ToArray();
                Check(order.Length is > 1 and < 64 && order.Distinct().Count() == order.Length &&
                    !order.Any(id => id is "Profile" or "X1" or "X2" or "USBLeft" or "USBRight"),
                    "the standard input choices contain duplicates or Adaptive-only ports.");
                for (int move = 0; move < order.Length; move++) await Invoke(view.Previous);
                Check(_selectedInput == order[0] && Peer(view.Previous).GetItemStatus() == "First input", "Previous did not reach/describe the first input.");
                await Invoke(view.Previous); Check(_selectedInput == order[0], "Previous wrapped around the first input.");
                for (int index = 1; index < order.Length; index++)
                {
                    await Invoke(view.Next);
                    Check(_selectedInput == order[index] && view.Picker.SelectedValue?.ToString() == order[index],
                        "Next skipped an actual displayed input choice on " + prefix);
                }
                Check(Peer(view.Next).GetItemStatus() == "Last input", "Next did not describe the last input.");
                await Invoke(view.Next); Check(_selectedInput == order[^1], "Next wrapped around the last input.");
                string selected = _selectedInput; await Invoke(view.Inspect);
                Check(_selectedInput == selected && Pages.SelectedIndex == page && Peer(view.Inspect).GetItemStatus().StartsWith("Selected.", StringComparison.Ordinal),
                    "inspecting a simplified input changed selection/page or lost its accessible selected state.");
                Check(AutomationProperties.GetLiveSetting(view.Announcement) == AutomationLiveSetting.Polite &&
                    !string.IsNullOrWhiteSpace(Peer(view.Announcement).GetName()),
                    "simplified input selection has no readable, polite announcement region.");
                report.AppendLine("PASS " + prefix + " one-control mode: named keyboard/UIA Invoke targets traverse all " + order.Length + " displayed standard choices once, clamp both ends and inspect without executing the assignment.");
            }

            _settings = _settings with
            {
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [new("A", "Blue foot switch", AccessoryKind.Pedal, [AccessorySignal.B])]
            };
            PopulateControls(); ReadSettings(); SelectPage(9); ShowPreview(); SetPortsView(true);
            var hardwareView = _simpleInputViews.Single(view => view.Prefix == "Hardware");
            var portsOrder = hardwareView.Picker.Items.Cast<VisualChoice<string>>().Select(choice => choice.Value).ToArray();
            Check(portsOrder.Length is > 1 and < 64 && Array.IndexOf(portsOrder, "A") >= 0,
                "the Adaptive port choices do not expose the saved A socket.");
            for (int move = 0; move < portsOrder.Length; move++) await Invoke(hardwareView.Previous);
            for (int index = 1; index <= Array.IndexOf(portsOrder, "A"); index++) await Invoke(hardwareView.Next);
            ShowPreview(new Gamepad(Buttons.B, 0, 0, 0, 0, 0, 0)); await Drain();
            Check(_selectedInput == "A" && !_presentedInputs["A"].IsActive && _presentedInputs["B"].IsActive &&
                InputTileState.GetIsActive(hardwareView.Inspect) && hardwareView.Action.Text == HumanAction(_presentedInputs["B"].Action) &&
                Peer(hardwareView.Inspect).GetName().Contains("Blue foot switch", StringComparison.Ordinal) &&
                Peer(hardwareView.Inspect).GetName().Contains("A socket", StringComparison.Ordinal) &&
                Peer(hardwareView.Inspect).GetItemStatus().Contains("Matching logical input active", StringComparison.Ordinal) &&
                hardwareView.State.Text.Contains("connection not reported", StringComparison.Ordinal),
                "a saved A socket emitting B did not expose B's actual action/activity and truthful socket information.");
            ShowPreview(); Check(!InputTileState.GetIsActive(hardwareView.Inspect), "the remapped saved socket retained activity after release.");
            report.AppendLine("PASS Adaptive A socket saved as a named switch emitting B: selected through actual Previous/Next Invoke, exposes B's action and activity, retains A socket/friendly name and unknown-connection warning, and clears on release.");

            SelectPage(3); await ToggleOneControl(false); await ChooseDirect(_motionChoice, (int)VisualMotion.Off);
            _settings = _settings with { ControllerType = ControllerPreference.Standard, AdaptiveAccessories = null };
            PopulateControls(); ReadSettings(); SetPortsView(false);
            SelectPage(0); ShowPreview(new Gamepad(Buttons.None, 0, 0, 20000, 0, 0, 0)); await Drain();
            var stick = LiveSurface.GetPart("LeftAxis") as ControllerPartButton ?? throw new InvalidOperationException("Visual Access live left stick is missing.");
            byte[] first = Pixels(stick); var logicalFirst = stick.AxisPosition;
            ShowPreview(new Gamepad(Buttons.None, 0, 0, 0, -20000, 0, 0)); await Drain();
            Check(stick.AxisPosition != logicalFirst && stick.HardwareActive && first.SequenceEqual(Pixels(stick)),
                "Motion Off moved the decorative stick cap or erased its changing logical value/active state.");
            report.AppendLine("PASS Motion Off keeps actual rendered stick pixels stable across two moving directions while distinct logical axis values and physical activity remain available.");

            _settings = _settings with
            {
                ControllerType = ControllerPreference.Adaptive,
                AdaptiveAccessories = [new("A", "Blue foot switch", AccessoryKind.Pedal, [AccessorySignal.A])]
            };
            PopulateControls(); ReadSettings(); SelectPage(0); ShowPreview();
            SelectPresentedInput("Y"); ShowPreview(new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0)); await Drain();
            Check(LastInputPanel.IsVisible && LastInputSummary.Text.Contains("Blue foot switch", StringComparison.Ordinal) &&
                Peer(LastInputSummary).GetName().Contains("Blue foot switch", StringComparison.Ordinal) && _selectedInput == "Y",
                "Last Input lost the saved friendly name, accessible name or separate configuration selection.");
            string last = LastInputSummary.Text; ShowPreview();
            Check(LastInputSummary.Text == last, "release erased the last friendly input summary.");
            SelectPage(3); await ChooseDirect(_complexityChoice, (int)VisualComplexity.Minimal); SelectPage(0); ShowPreview();
            Check(!LastInputPanel.IsVisible, "minimal complexity did not hide the optional Last Input region.");
            report.AppendLine("PASS optional Last Input exposes a saved friendly name/action to UI Automation, preserves configuration selection and the last press after release, and is hidden in minimal mode.");
            report.AppendLine("PASS isolated preview: no desktop output service, settings/profile file writes or native input. Actual Narrator speech, personal visual comfort and physical accessories remain manual acceptance.");
            File.WriteAllText(Path.Combine(output, "visual-access-verification.txt"), report.ToString());
        }
        finally
        {
            foreach (var selector in LayoutLogicalElements(Pages).OfType<ComboBox>()) selector.IsDropDownOpen = false;
            ProfileCombo.ActiveChoiceWindow?.Close();
            _settings = originalSettings; _app.Profiles = originalProfiles; _selectedInput = originalSelection;
            _loading = true;
            try { PopulateControls(); PopulateProfiles(); LoadBinding(); _app.ApplyVisualAccess(originalSettings); }
            finally { _loading = originalLoading; }
            Width = originalWidth; Height = originalHeight; WindowState = originalWindowState;
            SetPortsView(originalPorts); SelectPage(originalPage); UpdateAccessibleViewport();
            _lastPresentationSettings = null; RefreshStatus(originalStatus); UpdateLayout();
            _lastInputId = originalLastInput; _previousActiveInputs.Clear(); foreach (string input in originalActiveInputs) _previousActiveInputs.Add(input);
            HardwareInputDetails.IsExpanded = originalDetailsExpanded;
            if (refreshRunning) _refresh.Start(); if (saveRunning) _save.Start();
            await Drain();
        }
    }
}

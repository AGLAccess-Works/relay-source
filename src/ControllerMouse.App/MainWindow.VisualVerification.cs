using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void CheckDisconnectedGeometry(string output, StringBuilder report)
    {
        if (!ControlDisconnectedPane.IsVisible || ControlWorkspace.IsVisible || ControlTilesHost.IsVisible || StatusInstructionLine.IsVisible)
            throw new InvalidOperationException("Disconnected view shows stale live controller controls or hides connection help.");
        var labels = LayoutLogicalElements(ControlDisconnectedPane).OfType<TextBlock>().Where(text => text.IsVisible).ToArray();
        if (!labels.Any(text => text.Text == StatusTitle.Text) || !labels.Any(text => text.Text == StatusDetail.Text) ||
            string.IsNullOrWhiteSpace(StatusTitle.Text) || string.IsNullOrWhiteSpace(StatusDetail.Text))
            throw new InvalidOperationException("Disconnected view does not show the actual connection status and instructions.");
        foreach (var text in labels) CheckCriticalText(text, "disconnected desktop view");
        var buttons = LayoutLogicalElements(ControlDisconnectedPane).OfType<Button>().ToArray();
        if (buttons.Length != 2) throw new InvalidOperationException("Disconnected view must offer Hardware and connection help.");
        foreach (var button in buttons)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(button);
            if (button.ActualWidth < 44 || button.ActualHeight < 44 || peer == null || string.IsNullOrWhiteSpace(peer.GetName()) ||
                peer.GetPattern(PatternInterface.Invoke) == null || !button.Focus())
                throw new InvalidOperationException("Disconnected connection action is not a full-size named keyboard/UIA action.");
        }
        CaptureRelay(output, "control-disconnected-layout");
        var hardware = buttons.Single(button => button.Tag?.ToString() == "9");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(hardware)!.GetPattern(PatternInterface.Invoke)).Invoke();
        var frame = new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); UpdateLayout();
        if (Pages.SelectedIndex != 9) throw new InvalidOperationException("Disconnected Open Hardware action does not open its page.");
        report.AppendLine("Disconnected desktop: actual status/instructions visible, stale hero/tiles hidden, two named 44-pixel keyboard actions, Hardware opened through UI Automation.");
    }

    private void CheckShellGeometry(string context)
    {
        var window = new Rect(new Size(ActualWidth, ActualHeight));
        foreach (Button button in WindowActions.Children)
            if (button.ActualWidth < 44 || button.ActualHeight < 44 || button.ActualHeight > 48 ||
                !window.Contains(LayoutBounds(button, this)))
                throw new InvalidOperationException("Window caption controls are clipped or too small: " + context);
        var header = LayoutBounds(PageHeaderGrid, this);
        var viewport = LayoutBounds(AccessViewport, this);
        var navigation = LayoutBounds(NavigationViewport, this);
        if (header.IsEmpty || viewport.IsEmpty || header.Bottom > viewport.Top + .5 ||
            navigation.Right > viewport.Left + .5 || viewport.Right > ActualWidth + .5 || viewport.Bottom > ActualHeight + .5)
            throw new InvalidOperationException("Shell regions overlap or extend outside the window: " + context);
        CheckCriticalText(PageHeading, context);
        foreach (var text in LayoutVisualElements(PageHeaderActions).OfType<TextBlock>()) CheckCriticalText(text, context);
        foreach (Button button in PageHeaderActions.Children.OfType<Button>())
        {
            var rectangle = LayoutBounds(button, PageHeaderGrid);
            if (button.ActualWidth < 44 || button.ActualHeight < 44 || rectangle.Left < -.5 || rectangle.Right > PageHeaderGrid.ActualWidth + .5)
                throw new InvalidOperationException("Page header action is clipped: " + LayoutLabel(button) + " in " + context);
        }
        foreach (var radio in NavigationItems())
        {
            if (radio.ActualWidth < 44 || radio.ActualHeight < 44)
                throw new InvalidOperationException("Sidebar action is too small: " + LayoutLabel(radio) + " in " + context);
            foreach (var text in LayoutVisualElements(radio).OfType<TextBlock>()) CheckCriticalText(text, context);
        }
        bool compact = ActualHeight < 650 && InterfaceTextScale >= 1.5;
        if (compact)
        {
            var profile = LayoutBounds(ProfileCombo, NavigationViewport);
            if (profile.Left < -.5 || profile.Right > NavigationViewport.ViewportWidth + .5)
                throw new InvalidOperationException("The profile selector extends behind the sidebar scrollbar: " + context);
            foreach (var label in LayoutVisualElements(ProfileCombo).OfType<TextBlock>()) CheckCriticalText(label, context);
        }
        if (TryFindResource("PageTitleFontSize") is not double expected || Math.Abs(expected - 32 * InterfaceTextScale) > .01 ||
            (!compact && Math.Abs(PageHeading.FontSize - expected) > .01) ||
            compact && (PageHeading.FontSize < 16 * InterfaceTextScale || PageHeading.FontSize > expected || viewport.Height < 144))
            throw new InvalidOperationException("The page heading does not follow the selected text scale: " + context);
    }

    private void CheckDesktopCardsAndTiles(StringBuilder report)
    {
        foreach (var localScroll in LayoutVisualElements(LiveSurface).OfType<ScrollViewer>())
            if (localScroll.ScrollableHeight > 1 || localScroll.ScrollableWidth > 1 ||
                localScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible ||
                localScroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                throw VisualLayoutFailure("The desktop controller hero has an unnecessary internal scrollbar.");
        var cards = new[] { "ServiceStatusCard", "ProfileStatusCard", "DeviceStatusCard", "ConnectionStatusCard" }
            .Select(name => FindName(name) as FrameworkElement ?? throw new InvalidOperationException("Missing status card " + name)).ToArray();
        var rectangles = cards.Select(card => LayoutBounds(card, ControlStatusStrip)).ToArray();
        if (rectangles.Any(rectangle => rectangle.Width < 120 || rectangle.Height < 44 || rectangle.Height > 124) ||
            rectangles.Max(rectangle => rectangle.Top) - rectangles.Min(rectangle => rectangle.Top) > 1)
            throw VisualLayoutFailure("Desktop status cards are not a compact horizontal row.");
        for (int i = 0; i < cards.Length; i++)
        {
            foreach (var text in LayoutVisualElements(cards[i]).OfType<TextBlock>()) CheckCriticalText(text, "desktop status cards");
            for (int j = 0; j < i; j++)
            {
                var intersection = Rect.Intersect(rectangles[i], rectangles[j]);
                if (!intersection.IsEmpty && intersection.Width > .5 && intersection.Height > .5)
                    throw VisualLayoutFailure("Desktop status cards overlap.");
            }
        }
        string[] expected = ["A", "B", "X", "Y", "LeftAxis", "RightAxis", "Up", "Back", "Start"];
        var tiles = ControlTilesHost.Children.OfType<Button>().ToArray();
        if (tiles.Length != expected.Length || !expected.All(id => _controlTiles.ContainsKey(id)))
            throw VisualLayoutFailure("The compact desktop overview does not contain its nine primary inputs.");
        var bounds = tiles.Select(tile => LayoutBounds(tile, ControlTilesHost)).ToArray();
        if (bounds.Any(rectangle => rectangle.Width < 88 || rectangle.Height < 44 || rectangle.Height > 144) ||
            bounds.Count(rectangle => Math.Abs(rectangle.Top - bounds[0].Top) < 1) != expected.Length)
            throw VisualLayoutFailure("Desktop controller input tiles are oversized or do not form compact horizontal rows. " +
                $"Scale {_settings.InterfaceScale:0.00}; main font {TryFindResource("MainFontSize")}; caption font {TryFindResource("CaptionFontSize")}; tile host {ControlTilesHost.RenderSize}; " +
                string.Join(" | ", tiles.Select((tile, index) => $"{UIElementAutomationPeer.CreatePeerForElement(tile)?.GetAutomationId()}: {bounds[index]}; " +
                    string.Join("; ", LayoutVisualElements(tile).OfType<TextBlock>().Where(text => text.IsVisible).Select(text => $"'{text.Text}' font{text.FontSize:0.0} layout{text.ActualWidth:0.0}×{text.ActualHeight:0.0}")))));
        if (ControlTilesHost.ActualWidth < ControlVisualPane.ActualWidth + 160 ||
            LayoutBounds(ControlTilesHost, ControlWorkspace).Top < Math.Max(LayoutBounds(ControlVisualPane, ControlWorkspace).Bottom,
                LayoutBounds(ControlInspectorPane, ControlWorkspace).Bottom) - .5)
            throw VisualLayoutFailure("The input tiles do not span the workspace below the controller and inspector.");
        var scroll = (ScrollViewer)((TabItem)Pages.Items[0]).Content;
        scroll.ScrollToHome(); UpdateLayout();
        var content = (FrameworkElement)scroll.Content;
        var viewport = new Rect(content.TranslatePoint(new Point(), scroll), new Size(scroll.ViewportWidth, scroll.ViewportHeight));
        var artwork = LiveSurface.ArtworkRenderedBounds;
        var image = new Rect(LiveSurface.TranslatePoint(artwork.TopLeft, scroll), artwork.Size);
        if (artwork.IsEmpty || image.Left < viewport.Left - .5 || image.Top < viewport.Top - .5 ||
            image.Right > viewport.Right + .5 || image.Bottom > viewport.Bottom + .5)
            throw VisualLayoutFailure($"The desktop controller image is clipped at the initial page position: image {image}; viewport {viewport}.");
        foreach (var tile in tiles)
        {
            var rectangle = LayoutBounds(tile, scroll);
            if (rectangle.Left < viewport.Left - .5 || rectangle.Top < viewport.Top - .5 ||
                rectangle.Right > viewport.Right + .5 || rectangle.Bottom > viewport.Bottom + .5)
                throw VisualLayoutFailure($"The desktop input row requires scrolling before it can be read: {LayoutLabel(tile)}; tile {rectangle}; viewport {viewport}.");
        }
        string originalSelection = _selectedInput;
        var originalStatus = _lastPresentationStatus;
        try
        {
            foreach (string input in expected)
            {
                SelectPresentedInput(input); UpdateLayout(); scroll.ScrollToHome(); UpdateLayout();
                CheckStateRow(input + " selected");
            }
            if (originalStatus != null)
            {
                SelectPresentedInput("Y");
                RefreshStatus(originalStatus with { State = new ControllerMouse.Core.Gamepad(ControllerMouse.Core.Buttons.Y, 0, 0, 0, 0, 0, 0) });
                UpdateLayout(); scroll.ScrollToHome(); UpdateLayout();
                CheckStateRow("two-line action selected and physically pressed");
            }
            void CheckStateRow(string state)
            {
                foreach (var tile in tiles)
                {
                    var rectangle = LayoutBounds(tile, scroll);
                    if (tile.ActualHeight > 144 || rectangle.Left < viewport.Left - .5 || rectangle.Top < viewport.Top - .5 ||
                        rectangle.Right > viewport.Right + .5 || rectangle.Bottom > viewport.Bottom + .5)
                        throw VisualLayoutFailure($"Desktop input state changes clip the row: {state}; {LayoutLabel(tile)} at {rectangle}; viewport {viewport}.");
                    foreach (var text in LayoutVisualElements(tile).OfType<TextBlock>()) CheckCriticalText(text, "desktop input " + state);
                }
            }
        }
        finally
        {
            SelectPresentedInput(originalSelection);
            if (originalStatus != null) RefreshStatus(originalStatus);
            UpdateLayout();
        }
        report.AppendLine("Desktop input states: all nine inspected selections and the selected/pressed two-line Magnifier action retain a complete, readable initial row without scrolling.");
        report.AppendLine($"Desktop geometry: four status cards, {tiles.Length} input tiles, {bounds.Count(rectangle => Math.Abs(rectangle.Top - bounds[0].Top) < 1)} tiles in the first row; maximum tile height {bounds.Max(rectangle => rectangle.Height):0.0}.");
    }

    private void CheckHardwareInputEquivalents(string context)
    {
        foreach (string id in ControllerMouse.Core.ControlMapping.Controls.Select(option => option.Control.ToString()).Concat(new[] { "LeftAxis", "RightAxis", "Xbox" }))
        {
            if (!_hardwareTiles.TryGetValue(id, out var tile) || tile.Button is ControllerPartButton ||
                tile.Button.ActualWidth < 44 || tile.Button.ActualHeight < 44)
                throw new InvalidOperationException("Missing full-size hardware input alternative " + id + " in " + context);
            var peer = UIElementAutomationPeer.CreatePeerForElement(tile.Button);
            if (peer == null || peer.GetAutomationId() != "HardwareInput_" + id || string.IsNullOrWhiteSpace(peer.GetName()) ||
                peer.GetPattern(PatternInterface.Invoke) == null)
                throw new InvalidOperationException("Hardware input alternative has no named Invoke action: " + id + " in " + context);
        }
    }

    private void CheckPointerDiagramLabels(string context, StringBuilder report)
    {
        foreach (var plot in new[] { DeadZonePlot, ResponsePlot })
        {
            plot.InvalidateVisual(); UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(plot.ActualWidth), (int)Math.Ceiling(plot.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(plot);
            double expectedFont = (double)FindResource("CaptionFontSize");
            var labels = plot.RenderedLabels;
            if (labels.Count < (plot == DeadZonePlot ? 2 : 4))
                throw new InvalidOperationException("Pointer diagram labels were not rendered: " + context);
            for (int i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                if (Math.Abs(label.FontSize - expectedFont) > .01 || !new Rect(plot.RenderSize).Contains(label.Bounds))
                    throw new InvalidOperationException($"Pointer diagram label is clipped or shrunk: '{label.Text}' in {context}.");
                for (int j = 0; j < i; j++)
                    if (label.Bounds.IntersectsWith(labels[j].Bounds))
                        throw new InvalidOperationException($"Pointer diagram labels overlap: '{label.Text}' and '{labels[j].Text}' in {context}.");
            }
        }
        report.AppendLine($"Pointer diagrams: labels use the selected caption size without clipping or overlap in {context}.");
    }

    private void CheckMappingCategoryStability(string context, StringBuilder report)
    {
        string original = _actionCategory;
        var buttons = ActionCategoriesHost.Children.OfType<Button>().ToArray();
        var bounds = buttons.Select(button => LayoutBounds(button, ActionCategoriesHost)).ToArray();
        try
        {
            foreach (var selected in buttons)
            {
                selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); UpdateLayout();
                if (_actionCategory != System.Windows.Automation.AutomationProperties.GetName(selected) ||
                    buttons.Count(button => System.Windows.Automation.AutomationProperties.GetItemStatus(button) == "Selected category") != 1)
                    throw new InvalidOperationException("Mapping category selection is unclear: " + context);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var current = LayoutBounds(buttons[i], ActionCategoriesHost);
                    if (Math.Abs(current.X - bounds[i].X) > .5 || Math.Abs(current.Y - bounds[i].Y) > .5 ||
                        Math.Abs(current.Width - bounds[i].Width) > .5 || Math.Abs(current.Height - bounds[i].Height) > .5)
                        throw new InvalidOperationException("Mapping category targets move when selected: " + context);
                }
            }
        }
        finally { _actionCategory = original; BuildActionChoices(); UpdateLayout(); }
        report.AppendLine("Mapping categories: every selection keeps its target position and exposes one selected state in " + context + ".");
    }

    private void CheckVisualContrast(string theme, StringBuilder report)
    {
        Color Colour(string token) => TryFindResource(token) is SolidColorBrush { Opacity: 1 } brush && brush.Color.A == 255
            ? brush.Color : throw new InvalidOperationException("Contrast token is not an opaque solid brush: " + token);
        void Pair(string foreground, string background, double required)
        {
            double ratio = LayoutContrast(Colour(foreground), Colour(background));
            // Forced contrast resolves the user's Windows colours, even when their
            // chosen palette is below a WCAG ratio. Check that contract separately;
            // never silently substitute an app colour or certify a low system ratio.
            if (theme != "contrast" && ratio < required)
                throw new InvalidOperationException($"{theme} contrast {foreground}/{background}: {ratio:0.00}:1; needs {required:0.0}:1.");
            report.AppendLine($"{theme}: {foreground}/{background} {ratio:0.0000}:1." +
                (ratio < required ? " User's Windows colours preserved; below the app-palette target." : ""));
        }
        if (theme == "contrast")
        {
            foreach (var (token, expected) in new[] {
                ("SurfaceBackgroundBrush", SystemColors.WindowColor), ("TextPrimaryBrush", SystemColors.WindowTextColor),
                ("ActionFillBrush", SystemColors.HighlightColor), ("ActionTextBrush", SystemColors.HighlightTextColor),
                ("NavigationSelectionBrush", SystemColors.HighlightColor), ("NavigationSelectedTextBrush", SystemColors.HighlightTextColor),
                ("TextDisabledBrush", SystemColors.GrayTextColor) })
                if (Colour(token) != expected) throw new InvalidOperationException("Windows contrast colour was overridden: " + token);
            report.AppendLine("Windows contrast: window, text, action, navigation and disabled roles preserve system colours. Ratios below are measurements, not a conformance claim.");
        }
        foreach (string background in new[] { "SurfaceBackgroundBrush", "SurfaceRaisedBrush", "SurfaceSubtleBrush", "SurfaceStrongBrush", "SelectionSurfaceBrush" })
        { Pair("TextPrimaryBrush", background, 4.5); Pair("TextSecondaryBrush", background, 4.5); }
        Pair("OnAccentBrush", "AccentBrush", 4.5);
        Pair("SuccessBrush", "SuccessSurfaceBrush", 4.5);
        Pair("TextBrush", "ControlBrush", 4.5);
        Pair("BorderStrongBrush", "ControlBrush", 3);
        Pair("FocusRingBrush", "FocusInnerBrush", 3);
        foreach (string background in new[] { "SurfaceRaisedBrush", "SurfaceBackgroundBrush" })
        {
            double ratio = Math.Max(LayoutContrast(Colour("FocusRingBrush"), Colour(background)),
                LayoutContrast(Colour("FocusInnerBrush"), Colour(background)));
            if (theme != "contrast" && ratio < 3) throw new InvalidOperationException($"{theme} composed focus outline has insufficient contrast on {background}: {ratio:0.00}:1.");
            report.AppendLine($"{theme}: composed focus/{background} {ratio:0.00}:1.");
        }
    }

    private static double LayoutContrast(Color a, Color b)
    {
        static double Linear(byte channel) { double value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        double first = Luminance(a), second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    private static Rect LayoutBounds(FrameworkElement element, UIElement relative) => new(element.TranslatePoint(new Point(), relative), element.RenderSize);
    private static string LayoutLabel(System.Windows.Controls.Control control) => UIElementAutomationPeer.CreatePeerForElement(control)?.GetName() ?? control.Name ?? control.GetType().Name;
    private static IEnumerable<DependencyObject> LayoutLogicalElements(DependencyObject parent)
    {
        yield return parent;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            foreach (var descendant in LayoutLogicalElements(child)) yield return descendant;
    }
    private static IEnumerable<DependencyObject> LayoutVisualElements(DependencyObject parent)
    {
        yield return parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var descendant in LayoutVisualElements(VisualTreeHelper.GetChild(parent, i))) yield return descendant;
    }
    private static bool LayoutHasAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T) return true;
        return false;
    }
    private static void CheckCriticalText(TextBlock text, string context)
    {
        if (!text.IsVisible || string.IsNullOrWhiteSpace(text.Text)) return;
        if (text.FontFamily.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase) ||
            text.FontFamily.Source.Contains("Fluent", StringComparison.OrdinalIgnoreCase)) return;
        if (text.ActualWidth <= 0 || text.ActualHeight <= 0)
            throw new InvalidOperationException($"Critical label has no visible layout in {context}: '{text.Text}'.");
        double width = Math.Max(1, text.ActualWidth - text.Padding.Left - text.Padding.Right);
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
            Brushes.Black, null, TextOptions.GetTextFormattingMode(text), VisualTreeHelper.GetDpi(text).PixelsPerDip);
        if (text.TextWrapping != TextWrapping.NoWrap) formatted.MaxTextWidth = width;
        if (double.IsFinite(text.LineHeight)) formatted.LineHeight = text.LineHeight;
        double availableHeight = text.ActualHeight - text.Padding.Top - text.Padding.Bottom;
        if (formatted.Height > availableHeight + 3 ||
            text.TextWrapping == TextWrapping.NoWrap && formatted.WidthIncludingTrailingWhitespace > width + 1 ||
            text.TextTrimming != TextTrimming.None && formatted.WidthIncludingTrailingWhitespace > width + 1)
            throw new InvalidOperationException($"Critical label is clipped in {context}: '{text.Text}'; text {formatted.Width:0.0}×{formatted.Height:0.0}, available {width:0.0}×{availableHeight:0.0}.");
    }

    private InvalidOperationException VisualLayoutFailure(string message)
    {
        // Save the exact failed arrangement before the surrounding fixture restores it.
        // Use the same command-line output convention as the isolated smoke runner.
        if (_app.SmokeTest)
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(arguments, "--smoke-test");
                string output = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] :
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "controller-mouse-smoke");
                System.IO.Directory.CreateDirectory(output);
                CaptureRelay(output, "control-desktop-layout-failure");
                System.IO.File.WriteAllText(System.IO.Path.Combine(output, "control-desktop-layout-failure.txt"), message);
            }
            catch (Exception diagnosticError)
            {
                message += " Failure screenshot could not be saved: " + diagnosticError.Message;
            }
        }
        return new InvalidOperationException(message);
    }
}

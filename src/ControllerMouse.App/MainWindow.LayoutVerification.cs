using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void RunReflowChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Layout verification requires the isolated preview without desktop output.");
        var original = _settings;
        double originalWidth = Width, originalHeight = Height;
        int originalPage = Pages.SelectedIndex;
        var originalState = WindowState;
        bool originalLight = _app.LightTheme, originalContrast = _app.ContrastActive;
        int cases = 0, targets = 0;
        var report = new StringBuilder();
        try
        {
            WindowState = WindowState.Normal;
            _settings = _settings with { ControllerType = ControllerPreference.Standard, ShowControllerLabels = false };
            foreach (string theme in new[] { "light", "dark", "contrast" })
            {
                _app.LightTheme = theme == "light"; _app.ApplyTheme(theme == "contrast");
                CheckVisualContrast(theme, report);
            }
            _app.LightTheme = true; _app.ApplyTheme(false);
            var preview = new ControllerStatus(true, true, true, 0, "Battery medium", default, null, false);
            Width = 1440; Height = 900; _settings = _settings with { InterfaceScale = 1 };
            Pages.SelectedIndex = 0; UpdateAccessibleViewport(); RefreshStatus(preview with { Connected = false }); UpdateLayout();
            CheckDisconnectedGeometry(output, report);
            foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2 })
                foreach (var size in new[] { new Size(1440, 900), new Size(1366, 768), new Size(1920, 1080), new Size(640, 480) })
                {
                    _settings = _settings with { InterfaceScale = scale }; Width = size.Width; Height = size.Height;
                    foreach (int page in new[] { 0, 6, 9, 8, 3, 1, 2, 5, 7, 4, 10 })
                    {
                        Pages.SelectedIndex = page; UpdateAccessibleViewport(); RefreshStatus(preview); UpdateLayout();
                        var scroll = (ScrollViewer)((TabItem)Pages.Items[page]).Content;
                        scroll.ScrollToHome(); UpdateLayout();
                        string context = $"page {page} at {size.Width:0}×{size.Height:0} / {scale:P0}";
                        CheckShellGeometry(context);
                        if (!double.IsFinite(scroll.ExtentHeight) || !double.IsFinite(scroll.ExtentWidth) ||
                            scroll.ViewportHeight < 44 || scroll.ViewportWidth < 44 || AccessViewport.ScrollableWidth > 1 || scroll.ScrollableWidth > 1)
                            throw new InvalidOperationException($"Unreachable {context}: viewport {scroll.ViewportWidth:0.0}×{scroll.ViewportHeight:0.0}; horizontal overflow {scroll.ScrollableWidth:0.0}.");
                        if (scroll.Content is not FrameworkElement content)
                            throw new InvalidOperationException("Page content cannot be inspected: " + context);

                        foreach (var text in LayoutLogicalElements(content).OfType<TextBlock>().Where(text => text.IsVisible && !LayoutHasAncestor<ControllerSurface>(text)))
                        {
                            CheckCriticalText(text, context);
                            var rectangle = LayoutBounds(text, content);
                            if (rectangle.Left < -.5 || rectangle.Right > content.ActualWidth + .5)
                                throw new InvalidOperationException($"Horizontally clipped label in {context}: '{text.Text}'; {rectangle}; content width {content.ActualWidth:0.0}.");
                        }

                        var controls = LayoutLogicalElements(content).OfType<System.Windows.Controls.Control>()
                            .Where(control => control.IsVisible && control.Focusable &&
                                control is ButtonBase or ComboBox or Slider or TextBox or ListBox).ToArray();
                        foreach (var control in controls)
                        {
                            // Template rails are not standalone actions. Controller canvas parts
                            // have separate geometry and hit-testing checks.
                            if (control is ControllerPartButton || LayoutHasAncestor<ControllerSurface>(control)) continue;
                            var rectangle = LayoutBounds(control, content);
                            if (control.ActualWidth < 43.99 || control.ActualHeight < 43.99 ||
                                !double.IsFinite(rectangle.X) || !double.IsFinite(rectangle.Y))
                                throw new InvalidOperationException($"Small or invalid action target {LayoutLabel(control)} in {context}: {rectangle}.");
                            if (rectangle.Left < -.5 || rectangle.Right > content.ActualWidth + .5)
                                throw new InvalidOperationException($"Horizontally clipped action {LayoutLabel(control)} in {context}: {rectangle}; content width {content.ActualWidth:0.0}.");
                            foreach (var text in LayoutVisualElements(control).OfType<TextBlock>().Where(text => text.IsVisible))
                            {
                                CheckCriticalText(text, context);
                                var labelBounds = LayoutBounds(text, control);
                                if (labelBounds.Left < -.5 || labelBounds.Right > control.ActualWidth + .5 ||
                                    labelBounds.Top < -.5 || labelBounds.Bottom > control.ActualHeight + .5)
                                    throw new InvalidOperationException($"Action label overflows its target in {context}: {LayoutLabel(control)}; '{text.Text}' at {labelBounds}; control {control.RenderSize}.");
                            }
                            targets++;
                        }
                        var reachable = controls.Where(control => control.IsEnabled && control is not ControllerPartButton &&
                            !LayoutHasAncestor<ControllerSurface>(control)).ToArray();
                        foreach (var control in reachable.Take(1).Concat(reachable.TakeLast(1)).Distinct())
                        {
                            control.BringIntoView(); UpdateLayout();
                            if (!control.Focus()) throw new InvalidOperationException("Keyboard focus cannot reach " + LayoutLabel(control) + " in " + context);
                            UpdateLayout();
                            var visible = Rect.Intersect(LayoutBounds(control, scroll), new Rect(scroll.RenderSize));
                            if (visible.IsEmpty || visible.Width < 43.99 || visible.Height < 43.99)
                                throw new InvalidOperationException($"Focused action is hidden in {context}: {LayoutLabel(control)}; visible area {visible}.");
                        }
                        if (scroll.ScrollableHeight > 0)
                        {
                            scroll.ScrollToBottom(); UpdateLayout();
                            if (Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) > 1)
                                throw new InvalidOperationException("Page cannot scroll to its final content: " + context);
                        }
                        scroll.ScrollToHome(); UpdateLayout();
                        if (page == 0 && scale == 1 && size.Width == 1440)
                        {
                            CheckDesktopCardsAndTiles(report);
                            CaptureRelay(output, "control-polished-desktop");
                        }
                        if (page == 9) CheckHardwareInputEquivalents(context);
                        if (page == 1) CheckPointerDiagramLabels(context, report);
                        if (page == 6) CheckMappingCategoryStability(context, report);
                        if (page is 1 or 6 && scale == 1 && size.Width == 1440)
                            CaptureRelay(output, page == 1 ? "pointer-refined-desktop" : "mapping-refined-desktop");
                        if (page == 1 && scale == 2 && size.Width == 640)
                        {
                            DeadZonePlot.BringIntoView(); UpdateLayout();
                            CaptureRelay(output, "pointer-refined-small-200");
                            scroll.ScrollToHome(); UpdateLayout();
                        }
                        report.AppendLine($"PASS {context}: {controls.Length} logical controls; viewport {scroll.ViewportWidth:0.0}×{scroll.ViewportHeight:0.0}.");
                        cases++;
                    }
                    if (scale == 2 && size.Width == 640) { Pages.SelectedIndex = 7; UpdateLayout(); CaptureRelay(output, "settings-reflow-200-small"); }
                    if (scale == 1.5 && size.Width == 1366) { Pages.SelectedIndex = 6; UpdateLayout(); CaptureRelay(output, "mapping-reflow-150-laptop"); }
                    if (scale == 2 && size.Width == 1440) { Pages.SelectedIndex = 0; UpdateLayout(); CaptureRelay(output, "control-reflow-200-desktop"); }
                }
        }
        catch (Exception error)
        {
            // Preserve the exact failed page before restoring the user's preview state.
            try
            {
                CaptureRelay(output, "reflow-layout-failure");
                File.WriteAllText(Path.Combine(output, "reflow-layout-failure.txt"),
                    $"Page {Pages.SelectedIndex}; window {ActualWidth:0.0}×{ActualHeight:0.0}; text scale {_settings.InterfaceScale:P0}.\n{error}\n\nCompleted checks:\n{report}");
            }
            catch (Exception diagnosticError)
            {
                error.Data["ReflowDiagnosticError"] = diagnosticError.Message;
            }
            throw;
        }
        finally
        {
            _settings = original; _app.LightTheme = originalLight; _app.ApplyTheme(originalContrast);
            Width = originalWidth; Height = originalHeight; WindowState = originalState;
            PopulateControls(); Pages.SelectedIndex = originalPage; UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
        }
        report.Insert(0, $"PASS: {cases} page/size/text-scale combinations and {targets} action target checks. Eleven pages at 100–200% text and 1440×900, 1366×768, 1920×1080, 640×480. Critical shell/control labels fit their allocated bounds; logical actions remain at least 44 pixels and horizontally reachable; first/final controls receive visible keyboard focus; vertical page ends remain reachable. Desktop status cards and input tiles use compact rows below the controller/inspector. Light, dark and high-contrast text/control token pairs were measured. Individual controller canvases retain independent scrolling and surface tests. These automated checks do not certify spoken Narrator output, braille, physical switches or mixed-DPI hardware.\n\n");
        File.WriteAllText(Path.Combine(output, "reflow.txt"), report.ToString());
    }
}

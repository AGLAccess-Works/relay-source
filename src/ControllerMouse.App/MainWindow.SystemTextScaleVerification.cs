using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void RunWindowsTextScaleChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Windows text-size checks require an isolated preview.");
        var original = _settings;
        double originalWindows = _app.WindowsTextScale, originalWidth = Width, originalHeight = Height;
        var originalState = WindowState;
        int originalPage = Pages.SelectedIndex, cases = 0, targets = 0;
        var scrollOffsets = Pages.Items.Cast<TabItem>().Select(tab => (Scroll: (ScrollViewer)tab.Content,
            X: ((ScrollViewer)tab.Content).HorizontalOffset, Y: ((ScrollViewer)tab.Content).VerticalOffset)).ToArray();
        var report = new StringBuilder();
        try
        {
            WindowState = WindowState.Normal;
            _settings = original with { InterfaceScale = 1 };
            foreach (double windows in new[] { 1.25, 1.5, 2.25 })
                foreach (var size in new[] { new Size(1440, 900), new Size(640, 480) })
                {
                    Width = size.Width; Height = size.Height;
                    _app.SetVerificationWindowsTextScale(windows);
                    for (int page = 0; page < Pages.Items.Count; page++)
                    {
                        Pages.SelectedIndex = page; UpdateAccessibleViewport(); UpdateLayout();
                        var scroll = (ScrollViewer)((TabItem)Pages.Items[page]).Content;
                        scroll.ScrollToHome(); UpdateLayout();
                        string context = $"page {page}, Windows text {windows:P0}, {size.Width}×{size.Height}";
                        CheckShellGeometry(context);
                        foreach (var radio in NavigationItems())
                            foreach (var label in LayoutVisualElements(radio).OfType<TextBlock>().Where(t => t is not DecorativeGlyph && t.IsVisible))
                            {
                                var word = new FormattedText(label.Text, CultureInfo.CurrentUICulture, label.FlowDirection,
                                    new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch),
                                    label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                                if (word.WidthIncludingTrailingWhitespace > label.ActualWidth + 1)
                                    throw new InvalidOperationException($"Navigation word breaks at Windows text size: {label.Text}, {context}.");
                            }
                        if (_settings.InterfaceScale != 1 || Math.Abs(FontSize - 16 * windows) > .01 ||
                            Math.Abs((double)FindResource("MainFontSize") - 16 * windows) > .01 ||
                            scroll.ViewportHeight < 44 || scroll.ScrollableWidth > 1)
                            throw new InvalidOperationException("Windows text size was ignored, persisted or clipped: " + context);
                        if (scroll.Content is not FrameworkElement content) throw new InvalidOperationException(context);
                        foreach (var text in LayoutLogicalElements(content).OfType<TextBlock>().Where(t => t.IsVisible && !LayoutHasAncestor<ControllerSurface>(t)))
                            CheckCriticalText(text, context);
                        var controls = LayoutLogicalElements(content).OfType<System.Windows.Controls.Control>()
                            .Where(c => c.IsVisible && c.IsEnabled && c.Focusable && c is ButtonBase or ComboBox or Slider or TextBox or ListBox)
                            .Where(c => c is not ControllerPartButton && !LayoutHasAncestor<ControllerSurface>(c)).ToArray();
                        foreach (var control in controls)
                        {
                            var bounds = LayoutBounds(control, content);
                            if (control.ActualWidth < 43.99 || control.ActualHeight < 43.99 || bounds.Left < -.5 || bounds.Right > content.ActualWidth + .5)
                                throw new InvalidOperationException($"Windows text size clips {LayoutLabel(control)}: {context}.");
                            targets++;
                        }
                        foreach (var control in controls.Take(1).Concat(controls.TakeLast(1)).Distinct())
                        {
                            control.BringIntoView(); UpdateLayout();
                            if (!control.Focus()) throw new InvalidOperationException("Unreachable Windows text-size control: " + context);
                            UpdateLayout();
                            var visible = Rect.Intersect(LayoutBounds(control, scroll), new Rect(scroll.RenderSize));
                            if (visible.IsEmpty || visible.Width < 43.99 || visible.Height < 43.99)
                                throw new InvalidOperationException("Focused control is hidden at Windows text size: " + context);
                        }
                        report.AppendLine("PASS " + context);
                        cases++;
                    }
                    if (windows == 2.25 && size.Width == 640)
                    {
                        Pages.SelectedIndex = 3; UpdateAccessibleViewport(); UpdateLayout();
                        if (_systemTextSizeNotice?.IsVisible != true || !_systemTextSizeNotice.Text.Contains("225", StringComparison.Ordinal))
                            throw new InvalidOperationException("Windows text-size minimum is not explained.");
                        CaptureRelay(output, "windows-text-225-small");
                    }
                }
            _settings = original with { InterfaceScale = 1.8 };
            ThemeButton.BringIntoView(); UpdateLayout();
            if (!ThemeButton.Focus()) throw new InvalidOperationException("Text-size focus fixture could not focus its real action.");
            _app.SetVerificationWindowsTextScale(1.5);
            if (Math.Abs(FontSize - 28.8) > .01) throw new InvalidOperationException("A larger Relay preference was reduced.");
            _app.SetVerificationWindowsTextScale(2.25);
            _app.SetVerificationWindowsTextScale(1);
            if (_settings.InterfaceScale != 1.8 || Math.Abs(FontSize - 28.8) > .01 || !ThemeButton.IsKeyboardFocused)
                throw new InvalidOperationException("Returning Windows text size changed the saved Relay preference or lost keyboard focus.");
            report.Insert(0, $"PASS: {cases} page/window/Windows-text-size cases; {targets} targets. Injected Windows factors 125%, 150%, 225%; saved Relay preference preserved. No OS setting changes, native input or live Windows event delivery claimed.\n");
            File.WriteAllText(Path.Combine(output, "windows-text-size.txt"), report.ToString());
        }
        finally
        {
            _settings = original; _app.SetVerificationWindowsTextScale(originalWindows);
            Width = originalWidth; Height = originalHeight; WindowState = originalState;
            Pages.SelectedIndex = originalPage; UpdateAccessibleViewport(); UpdateLayout();
            foreach (var offset in scrollOffsets)
            {
                offset.Scroll.ScrollToHorizontalOffset(offset.X);
                offset.Scroll.ScrollToVerticalOffset(offset.Y);
            }
            UpdateLayout();
        }
    }
}

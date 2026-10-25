using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using ControllerMouse.Core;
using Microsoft.Win32;

namespace ControllerMouse.App;

internal sealed class InputDiagnosticsWindow : Window
{
    private readonly Func<string> _snapshot;
    private readonly InputDiagnostics _events;
    private readonly TextBox _state = TextArea("Input state and mappings"), _log = TextArea("Diagnostic event log");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly CheckBox _record, _live;
    internal InputDiagnosticsWindow(Func<string> snapshot, InputDiagnostics events, Action release)
    {
        _snapshot = snapshot; _events = events;
        Title = "Relay input diagnostics"; Width = 920; Height = 740; MinWidth = 480; MinHeight = 400;
        SetResourceReference(FontSizeProperty, "MainFontSize"); SetResourceReference(FontFamilyProperty, "InterfaceFontFamily");
        AccessibilitySupport.EnableKeyboardAccess(this);
        SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        var root = new StackPanel { Margin = new Thickness(20) };
        var header = new StackPanel();
        var heading = new TextBlock { Text = "Developer input diagnostics", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        heading.SetResourceReference(TextBlock.FontSizeProperty, "SectionFontSize"); AccessibilitySupport.SectionHeading(heading, AutomationHeadingLevel.Level1);
        header.Children.Add(heading);
        header.Children.Add(new TextBlock { Text = "Logical controller inputs only. Recording is opt-in and limited to 1,000 events. Typed text and screen content are omitted. Refresh manually for a stable screen-reader view.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
        _record = new CheckBox { Content = "Record diagnostic events", IsChecked = events.Enabled };
        _record.Checked += (_, _) => events.Enabled = true;
        _record.Unchecked += (_, _) => events.Enabled = false;
        _live = new CheckBox { Content = "Refresh this view automatically", IsChecked = false };
        header.Children.Add(_record); header.Children.Add(_live); root.Children.Add(header);
        var tabs = new TabControl { Margin = new Thickness(0, 12, 0, 12), Height = 320 };
        foreach (var tab in new[] { new TabItem { Header = "State and mappings", Content = _state }, new TabItem { Header = "Events", Content = _log } })
        {
            tab.SetResourceReference(FontSizeProperty, "MainFontSize"); tab.SetResourceReference(MinHeightProperty, "ControlMinHeight");
            tabs.Items.Add(tab);
        }
        root.Children.Add(tabs);
        var footer = new WrapPanel();
        Button Button(string name, Action action)
        {
            var button = new Button { Content = name, Margin = new Thickness(0, 0, 12, 8), MinHeight = 44 };
            AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); footer.Children.Add(button); return button;
        }
        var refresh = Button("Refresh", Refresh);
        Button("Release all Relay inputs", () => { release(); Refresh(); });
        Button("Clear events", () => { events.Clear(); Refresh(); });
        Button("Save diagnostics…", () =>
        {
            Refresh(); var dialog = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "Relay-input-diagnostics.txt" };
            if (dialog.ShowDialog(this) != true) return;
            try { File.WriteAllText(dialog.FileName, _state.Text + Environment.NewLine + _log.Text); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Could not save diagnostics: " + ex.Message, "Relay diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        Button("Close", Close).IsCancel = true;
        root.Children.Add(footer); Content = new ScrollViewer
        {
            Content = root,
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _timer.Tick += (_, _) => { if (_live.IsChecked == true) Refresh(); };
        Loaded += (_, _) => { Refresh(); refresh.Focus(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
    }
    private static TextBox TextArea(string name)
    {
        var text = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(8)
        };
        text.SetResourceReference(TextBox.FontSizeProperty, "CaptionFontSize");
        AutomationProperties.SetName(text, name); return text;
    }
    internal void Refresh()
    {
        _state.Text = _snapshot();
        _log.Text = string.Join(Environment.NewLine, _events.Snapshot().Select(e => $"{e.Time:HH:mm:ss.fff} {e.Level.ToString().ToUpperInvariant()} {e.Stage} {e.Detail}"));
    }
}

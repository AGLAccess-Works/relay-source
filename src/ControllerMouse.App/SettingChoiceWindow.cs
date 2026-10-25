using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControllerMouse.App;

internal sealed class SettingChoiceWindow : Window
{
    internal IReadOnlyList<Button> ChoiceButtons { get; }
    internal Button CancelButton { get; }
    internal ScrollViewer ChoicesViewport { get; }
    internal TextBlock Heading { get; }
    internal int SelectedIndex { get; private set; }
    internal object? SelectedValue { get; private set; }

    internal SettingChoiceWindow(Window owner, SettingChoiceButton setting)
    {
        Owner = owner;
        Title = setting.SettingName;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.CanResize;
        MinWidth = 320; MinHeight = 240;
        MaxWidth = Math.Max(MinWidth, owner.ActualWidth - 48);
        MaxHeight = Math.Max(MinHeight, owner.ActualHeight - 48);
        Width = Math.Min(660, MaxWidth);
        double textSize = owner.TryFindResource("MainFontSize") is double size ? size : 16;
        Height = Math.Min(MaxHeight, Math.Max(300, 168 + setting.Items.Count * (textSize * 3 + 24)));
        FontFamily = owner.FontFamily;
        SetResourceReference(FontSizeProperty, "MainFontSize");
        SetResourceReference(BackgroundProperty, "SurfaceRaisedBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        string fieldId = AutomationProperties.GetAutomationId(setting);
        if (string.IsNullOrEmpty(fieldId)) fieldId = "SettingChoice";
        AutomationProperties.SetName(this, "Choose " + setting.SettingName.ToLowerInvariant());
        AutomationProperties.SetAutomationId(this, fieldId + "Choices");
        AccessibilitySupport.EnableKeyboardAccess(this);
        SelectedIndex = setting.SelectedIndex;
        SelectedValue = setting.SelectedValue;

        var body = new Grid { Margin = new Thickness(4) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var introduction = new StackPanel();
        Heading = new TextBlock
        {
            Text = setting.SettingName,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Heading.SetResourceReference(TextBlock.FontSizeProperty, "SectionFontSize");
        AccessibilitySupport.SectionHeading(Heading, AutomationHeadingLevel.Level1);
        AutomationProperties.SetAutomationId(Heading, fieldId + "ChoiceHeading");
        introduction.Children.Add(Heading);
        var help = new TextBlock
        {
            Text = "Choose an option to use it. Cancel keeps your current choice.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        help.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        help.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        introduction.Children.Add(help); body.Children.Add(introduction);

        var list = new StackPanel { Margin = new Thickness(4) };
        var buttons = new List<Button>();
        for (int index = 0; index < setting.Items.Count; index++)
        {
            object item = setting.Items[index];
            object? optionValue = setting.ChoiceValue(item);
            string name = setting.DisplayName(item), description = setting.Description(item);
            bool current = index == setting.SelectedIndex;
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinWidth = 44,
                Margin = new Thickness(0, 0, 0, 10),
                Tag = index
            };
            button.SetResourceReference(MinHeightProperty, "ControlMinHeight");
            button.SetResourceReference(FontSizeProperty, "MainFontSize");
            if (current)
            {
                button.SetResourceReference(BackgroundProperty, "SelectionSurfaceBrush");
                button.SetResourceReference(ForegroundProperty, "SelectionTextBrush");
                button.SetResourceReference(BorderBrushProperty, "SelectedInnerBrush");
                button.BorderThickness = new Thickness(2);
            }
            var content = new StackPanel();
            var label = new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            content.Children.Add(label);
            if (!string.IsNullOrEmpty(description))
            {
                var details = new TextBlock
                {
                    Text = description,
                    FontWeight = FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                details.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
                content.Children.Add(details);
            }
            if (current)
            {
                var status = new TextBlock
                {
                    Text = "✓ Current choice",
                    FontWeight = FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                status.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
                content.Children.Add(status);
            }
            button.Content = content;
            AutomationProperties.SetName(button, name);
            AutomationProperties.SetItemStatus(button, current ? "Current choice" : "Available choice");
            AutomationProperties.SetAutomationId(button, fieldId + "Choice" + index);
            AutomationProperties.SetHelpText(button, string.IsNullOrEmpty(description)
                ? "Use this choice and return to settings."
                : description + " Use this choice and return to settings.");
            button.Click += (_, _) => { SelectedIndex = (int)button.Tag; SelectedValue = optionValue; DialogResult = true; };
            button.GotKeyboardFocus += (_, _) => button.BringIntoView();
            buttons.Add(button); list.Children.Add(button);
        }
        ChoiceButtons = buttons;
        Grid.SetRow(list, 1); body.Children.Add(list);
        CancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 44,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 0)
        };
        CancelButton.SetResourceReference(MinHeightProperty, "ControlMinHeight");
        CancelButton.SetResourceReference(FontSizeProperty, "MainFontSize");
        AutomationProperties.SetName(CancelButton, "Cancel " + setting.SettingName.ToLowerInvariant() + " change");
        AutomationProperties.SetAutomationId(CancelButton, fieldId + "ChoiceCancel");
        AutomationProperties.SetHelpText(CancelButton, "Keep the current choice and return to settings.");
        CancelButton.Click += (_, _) => DialogResult = false;
        Grid.SetRow(CancelButton, 2); body.Children.Add(CancelButton);
        // One scroll surface leaves enough space to read a complete option at
        // 200% text on a small display. The heading and Cancel remain reachable
        // in the same reading order, without squeezing the choice list between them.
        ChoicesViewport = new ScrollViewer
        {
            Content = body,
            Margin = new Thickness(20),
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Content = ChoicesViewport;
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (!IsVisible) return;
            Button initial = buttons.Count == 0 ? CancelButton : buttons[Math.Clamp(setting.SelectedIndex, 0, buttons.Count - 1)];
            initial.BringIntoView(); initial.Focus();
        }));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; return; }
            // Keyboard arrows explore the same order as the controller, including
            // Cancel. Moving focus never changes the currently saved setting.
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down or Key.Left or Key.Right &&
                Keyboard.FocusedElement is Button arrowSource && (buttons.Contains(arrowSource) || arrowSource == CancelButton))
            {
                int current = arrowSource == CancelButton ? buttons.Count : buttons.IndexOf(arrowSource);
                int targetIndex = Math.Clamp(current + (e.Key is Key.Down or Key.Right ? 1 : -1), 0, buttons.Count);
                Button target = targetIndex == buttons.Count ? CancelButton : buttons[targetIndex];
                target.BringIntoView(); target.Focus(); e.Handled = true; return;
            }
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            if (!e.IsRepeat && Keyboard.FocusedElement is Button focused && (buttons.Contains(focused) || focused == CancelButton))
                focused.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, focused));
            e.Handled = true;
        };
    }
}

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControllerMouse.App;

internal sealed class ProfileDeleteConfirmation : Window
{
    private static readonly DependencyProperty DestructiveFillProperty = DependencyProperty.Register(
        "DestructiveFill", typeof(Brush), typeof(ProfileDeleteConfirmation),
        new PropertyMetadata(null, (owner, change) =>
            ((ProfileDeleteConfirmation)owner).UpdateDeleteInk(change.NewValue as Brush)));
    internal Button CancelButton { get; }
    internal Button DeleteButton { get; }
    internal TextBlock Heading { get; }
    internal ScrollViewer DetailsViewport { get; }

    internal static bool Confirm(Window owner, string profileName) =>
        new ProfileDeleteConfirmation(owner, profileName).ShowDialog() == true;

    private ProfileDeleteConfirmation(Window owner, string profileName)
    {
        Owner = owner;
        Title = "Delete profile";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.CanResize;
        MinWidth = 360;
        MinHeight = 220;
        MaxWidth = Math.Max(MinWidth, owner.ActualWidth - 32);
        MaxHeight = Math.Max(MinHeight, owner.ActualHeight - 32);
        Width = Math.Min(560, MaxWidth);
        SizeToContent = SizeToContent.Height;
        FontFamily = owner.FontFamily;
        SetResourceReference(FontSizeProperty, "MainFontSize");
        SetResourceReference(BackgroundProperty, "SurfaceRaisedBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        AutomationProperties.SetName(this, "Delete profile confirmation");
        AutomationProperties.SetAutomationId(this, "ProfileDeleteConfirmation");
        AccessibilitySupport.EnableKeyboardAccess(this);

        var body = new Grid { Margin = new Thickness(24) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new());
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Heading = new TextBlock
        {
            Text = "Delete this profile?",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap
        };
        Heading.SetResourceReference(TextBlock.FontSizeProperty, "SectionFontSize");
        AutomationProperties.SetAutomationId(Heading, "ProfileDeleteHeading");
        AccessibilitySupport.Heading(Heading);
        var headingHost = new Grid { Margin = new Thickness(-6, 0, 0, 0) };
        headingHost.Children.Add(new Border
        {
            Child = Heading,
            BorderThickness = new Thickness(3),
            Padding = new Thickness(3, 0, 3, 0),
            BorderBrush = Brushes.Transparent
        });
        foreach (var (name, colour, thickness, inset) in new[] {
            ("ProfileDeleteHeadingFocusOuter", "FocusRingBrush", "FocusOuterThickness", false),
            ("ProfileDeleteHeadingFocusInner", "FocusInnerBrush", "FocusInnerThickness", true) })
        {
            var ring = new Border { Name = name, CornerRadius = new CornerRadius(inset ? 4 : 6), IsHitTestVisible = false };
            ring.SetResourceReference(Border.BorderThicknessProperty, thickness);
            if (inset) ring.SetResourceReference(MarginProperty, "FocusOuterThickness");
            var headingStyle = new Style(typeof(Border));
            headingStyle.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Transparent));
            var focusedHeading = new DataTrigger { Binding = new Binding(nameof(IsKeyboardFocused)) { Source = Heading }, Value = true };
            focusedHeading.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension(colour)));
            headingStyle.Triggers.Add(focusedHeading); ring.Style = headingStyle;
            headingHost.Children.Add(ring);
        }
        body.Children.Add(headingHost);

        var details = new StackPanel();
        var question = new TextBlock
        {
            Text = $"Delete ‘{profileName}’ and its saved settings?",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        question.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
        AutomationProperties.SetAutomationId(question, "ProfileDeleteQuestion");
        details.Children.Add(question);
        var help = new TextBlock
        {
            Text = "Other profiles stay saved. Choose Cancel to keep this profile.",
            TextWrapping = TextWrapping.Wrap
        };
        help.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
        help.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        AutomationProperties.SetAutomationId(help, "ProfileDeleteHelp");
        details.Children.Add(help);
        DetailsViewport = new ScrollViewer
        {
            Content = details,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false
        };
        Grid.SetRow(DetailsViewport, 1); body.Children.Add(DetailsViewport);

        CancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 44,
            MinHeight = 44,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DeleteButton = new Button { Content = "Delete profile", IsDefault = false, MinWidth = 44, MinHeight = 44 };
        DeleteButton.SetResourceReference(BackgroundProperty, "ErrorBrush");
        DeleteButton.SetResourceReference(BorderBrushProperty, "ErrorBrush");
        SetResourceReference(DestructiveFillProperty, "ErrorBrush");
        foreach (var button in new[] { CancelButton, DeleteButton }) button.SetResourceReference(FontSizeProperty, "MainFontSize");
        AutomationProperties.SetName(CancelButton, "Cancel profile deletion");
        AutomationProperties.SetAutomationId(CancelButton, "CancelProfileDeletion");
        AutomationProperties.SetHelpText(CancelButton, "Keep this profile and close the confirmation.");
        AutomationProperties.SetName(DeleteButton, "Delete profile " + profileName);
        AutomationProperties.SetAutomationId(DeleteButton, "ConfirmProfileDeletion");
        AutomationProperties.SetHelpText(DeleteButton, "Delete this profile and its saved settings. Other profiles are kept.");
        CancelButton.Click += (_, _) => DialogResult = false;
        DeleteButton.Click += (_, _) => DialogResult = true;
        var actions = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        actions.Children.Add(CancelButton); actions.Children.Add(DeleteButton);
        Grid.SetRow(actions, 2); body.Children.Add(actions);
        Content = body;

        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CancelButton.Focus()));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            // There is no window-wide destructive default. Enter only operates the focused button.
            if (!e.IsRepeat && Keyboard.FocusedElement is Button focused &&
                (focused == CancelButton || focused == DeleteButton))
                focused.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, focused));
            e.Handled = true;
        };
    }

    private void UpdateDeleteInk(Brush? fill)
    {
        if (Application.Current is App { ContrastActive: true })
        {
            DeleteButton.SetResourceReference(ForegroundProperty, SystemColors.WindowBrushKey);
            return;
        }
        // Custom schemes can change every surrounding surface. Choose ink against the
        // destructive fill itself, so its label remains readable in every scheme.
        if (fill is SolidColorBrush solid)
        {
            Color ink = VisualAccessTheme.Contrast(solid.Color, Colors.White) >=
                VisualAccessTheme.Contrast(solid.Color, Colors.Black) ? Colors.White : Colors.Black;
            var brush = new SolidColorBrush(ink); brush.Freeze(); DeleteButton.Foreground = brush;
        }
        else DeleteButton.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
    }
}

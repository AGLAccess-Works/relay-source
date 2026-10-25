using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void NavigationRailSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (NavigationRailLayout != null)
            NavigationRailLayout.MinHeight = Math.Max(0, e.NewSize.Height - 32);
    }
    private void ReflowSecondaryPages(double width, double scale)
    {
        if (PageHeaderGrid == null || PointerWorkbench == null) return;
        double available = Math.Max(0, width - 48);
        double space = 24;
        double safeContentWidth = Math.Max(1, available - 44);
        foreach (var navigationItem in NavigationItems()) navigationItem.MinHeight = Math.Max(50, 44 * scale);
        double statusWidth = Math.Max(1, safeContentWidth - 18);
        foreach (var card in new[] { ServiceStatusCard, ProfileStatusCard, DeviceStatusCard, ConnectionStatusCard })
        {
            double ideal = card == DeviceStatusCard ? 244 : card == ConnectionStatusCard ? 300 : card == ProfileStatusCard ? 204 : 176;
            card.Width = Math.Min(ideal * scale, statusWidth);
            card.MinWidth = Math.Min(144 * scale, statusWidth);
            card.MaxWidth = statusWidth;
            if (card.Child is Grid cardLayout)
                cardLayout.ColumnDefinitions[0].Width = new GridLength((card == ServiceStatusCard ? 30 : 34) * Math.Min(2, scale));
        }
        double workbenchWidth = Math.Max(1, safeContentWidth - 24);
        double inspectorWidth = Math.Max(290 * scale, workbenchWidth * .32);
        bool workbenchStacked = workbenchWidth - inspectorWidth < 640;
        foreach (string prefix in new[] { "Control", "Mapping", "Hardware" })
        {
            if (FindName(prefix + "Workbench") is not Grid workbench || FindName(prefix + "VisualPane") is not FrameworkElement visual ||
                FindName(prefix + "InspectorPane") is not FrameworkElement inspector) continue;
            bool stacked = workbenchStacked || _portsView && prefix != "Mapping";
            if (FindName(prefix + "InspectorColumn") is ColumnDefinition column) column.Width = new GridLength(stacked ? 0 : inspectorWidth);
            workbench.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 24);
            Position(visual, 0, 0); Grid.SetColumnSpan(visual, stacked ? 3 : 1);
            Position(inspector, stacked ? 0 : 2, stacked ? 1 : 0); Grid.SetColumnSpan(inspector, stacked ? 3 : 1);
            inspector.Margin = new Thickness(0, stacked ? 20 : 0, 0, 0);
            if (prefix == "Control" && inspector is Border frame)
            {
                frame.BorderThickness = new Thickness(1);
                frame.Padding = new Thickness(16);
            }
        }
        bool compactHeader = ActualHeight < 650 && scale >= 1.5;
        UpdateCompactFooter();
        // Keep the page name readable at the body-text size in a short window,
        // leaving room for its controls instead of a large wrapped title.
        if (compactHeader) PageHeading.FontSize = Math.Max(32, 16 * scale);
        else PageHeading.SetResourceReference(TextBlock.FontSizeProperty, "PageTitleFontSize");
        // A large-text small window keeps its header short enough to leave a page viewport.
        // The same accessible profile selector moves into the scrollable rail.
        Panel profileHost = compactHeader ? NavigationProfileHost : PageHeaderActions;
        if (ProfileCombo.Parent is Panel oldProfileHost && oldProfileHost != profileHost)
        {
            oldProfileHost.Children.Remove(ProfileCombo);
            if (compactHeader) profileHost.Children.Add(ProfileCombo);
            else profileHost.Children.Insert(0, ProfileCombo);
        }
        NavigationProfileHost.Visibility = compactHeader ? Visibility.Visible : Visibility.Collapsed;
        PageHeaderPanel.Padding = compactHeader ? new Thickness(24, 4, 24, 4) : new Thickness(24, 10, 24, 12);
        PageContext.Visibility = compactHeader ? Visibility.Collapsed : Visibility.Visible;
        StatusBadge.Visibility = compactHeader || Pages.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (Button headerAction in PageHeaderActions.Children.OfType<Button>())
        {
            if (compactHeader) headerAction.MinHeight = 44;
            else headerAction.ClearValue(MinHeightProperty);
        }
        UpdateQuickThemeAction();

        // Keep the page title and window actions available when text grows.
        bool headerStacked = available < 760 * scale;
        PageHeaderGapColumn.Width = new GridLength(headerStacked ? 0 : 20);
        PageHeaderActionsColumn.Width = headerStacked ? new GridLength(0) : GridLength.Auto;
        Grid.SetColumn(PageHeaderActions, headerStacked ? 0 : 2);
        Grid.SetRow(PageHeaderActions, headerStacked ? 1 : 0);
        Grid.SetColumnSpan(PageHeaderActions, headerStacked ? 3 : 1);
        Grid.SetColumnSpan(PageHeaderTitlePane, headerStacked ? 3 : 1);
        PageHeaderActions.MaxWidth = Math.Max(1, available);
        // Stretch inside the rail's actual viewport. A width based on the column
        // ignores its scrollbar and crops profile names at large text sizes.
        ProfileCombo.Width = compactHeader ? double.NaN : Math.Min(180 * Math.Min(scale, 1.5), Math.Max(44, available - 8));
        PageHeaderActions.Margin = headerStacked ? new Thickness(0, compactHeader ? 4 : 12, 0, 0) : new Thickness(0);

        bool pointerStacked = available < 960 * scale;
        PointerFirstColumn.Width = new GridLength(1, GridUnitType.Star);
        PointerSecondColumn.Width = pointerStacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PointerThirdColumn.Width = pointerStacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Position(PointerDeadZonePane, 0, 0);
        Position(PointerResponsePane, pointerStacked ? 0 : 1, pointerStacked ? 1 : 0);
        Position(PointerFinePane, pointerStacked ? 0 : 2, pointerStacked ? 2 : 0);
        PointerDeadZonePane.Margin = pointerStacked ? new Thickness(0, 0, 0, space) : new Thickness(0, 0, 22, 0);
        PointerResponsePane.Padding = pointerStacked ? new Thickness(0, space, 0, space) : new Thickness(22, 0, 22, 0);
        PointerResponsePane.BorderThickness = pointerStacked ? new Thickness(0, 1, 0, 1) : new Thickness(1, 0, 1, 0);
        PointerFinePane.Margin = pointerStacked ? new Thickness(0, space, 0, 0) : new Thickness(22, 0, 0, 0);

        ReflowPair(AssistanceWorkbench, AssistanceMotionPane, AssistanceTargetPane, available < 880 * scale,
            new GridLength(1, GridUnitType.Star));
        ReflowPair(ScrollingWorkbench, ScrollSettingsPane, FeedbackSettingsPane, available < 920 * scale,
            new GridLength(1, GridUnitType.Star));
        bool profilesNeedStack = available < 900 * scale;
        ReflowPair(ProfilesWorkbench, ProfilesListPane, ProfilesEditorPane, profilesNeedStack,
            new GridLength(1, GridUnitType.Star));
        ProfilesEditorColumn.Width = profilesNeedStack ? new GridLength(0) : new GridLength(360 * scale);
        bool profilesStacked = Grid.GetRow(ProfilesEditorPane) == 1;
        ProfilesEditorPane.Padding = profilesStacked ? new Thickness(0, 20, 0, 0) : new Thickness(24, 0, 0, 0);
        ProfilesEditorPane.BorderThickness = profilesStacked ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);

        double mappingVisualWidth = workbenchStacked ? safeContentWidth : workbenchWidth - inspectorWidth;
        bool categoriesNeedStack = mappingVisualWidth < 640 + 134 * scale + 12;
        MappingInputsCategoryColumn.Width = categoriesNeedStack ? new GridLength(1, GridUnitType.Star) : new GridLength(134 * scale);
        MappingInputWorkbench.ColumnDefinitions[1].Width = new GridLength(categoriesNeedStack ? 0 : 12);
        MappingInputsSurfaceColumn.Width = categoriesNeedStack ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Position(ActionCategoriesHost, 0, 0);
        foreach (var category in ActionCategoriesHost.Children.OfType<Button>())
        {
            // Use a compact wrapping row above the diagram, or a full-width
            // vertical list when the category column has room beside it.
            category.Width = categoriesNeedStack ? double.NaN : Math.Max(44, 134 * scale - 8);
            category.MaxWidth = Math.Max(44, (categoriesNeedStack ? mappingVisualWidth : 134 * scale) - 8);
        }
        Position(MappingCanvasPane, categoriesNeedStack ? 0 : 2, categoriesNeedStack ? 1 : 0);
        MappingCanvasPane.Margin = new Thickness(0, categoriesNeedStack ? 16 : 0, 0, 0);
        MappingCategoryColumn.Width = new GridLength(0);
        MappingActionWorkbench.ColumnDefinitions[1].Width = new GridLength(0);
        MappingChoicesColumn.Width = new GridLength(1, GridUnitType.Star);
        Position(MappingChoicesHost, 2, 0);

        PracticeTargets.Columns = Math.Clamp((int)(available / Math.Max(1, 160 * scale)), 1, 5);
        PracticeTargets.Height = double.NaN;
        QuickTools.Columns = Math.Clamp((int)(available / Math.Max(1, 260 * scale)), 1, 3);
    }

    private void UpdateCompactFooter()
    {
        if (FooterPanel == null || Footer == null) return;
        bool routine = Footer.Text is "Settings saved on this PC" or "Settings saved locally · No account needed";
        bool wasVisible = FooterPanel.Visibility == Visibility.Visible;
        // A routine privacy/save reminder must not displace usable page content.
        // Errors and warnings stay visible; the same privacy information is in About.
        FooterPanel.Visibility = ActualHeight < 650 && InterfaceTextScale >= 1.5 && routine
            ? Visibility.Collapsed : Visibility.Visible;
        if (!wasVisible && FooterPanel.Visibility == Visibility.Visible && !routine)
            AccessibilitySupport.AnnounceAgain(Footer);
    }

    private void UpdateQuickThemeAction()
    {
        if (ThemeButton == null) return;
        bool compact = ActualHeight < 650 && InterfaceTextScale >= 1.5;
        bool dark = _settings.EffectiveVisualAccess.IsDark;
        string action = dark ? "Use light theme" : "Use dark theme";
        ThemeButton.Content = compact ? dark ? "Light theme" : "Dark theme" : action;
        AutomationProperties.SetName(ThemeButton, action);
        AutomationProperties.SetItemStatus(ThemeButton, dark ? "Current theme: Dark" : "Current theme: Light");
        ThemeButton.ToolTip = action;
    }

    private static void Position(FrameworkElement element, int column, int row)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
    }

    private static void ReflowPair(Grid grid, FrameworkElement first, FrameworkElement second,
        bool stacked, GridLength firstWidth)
    {
        grid.ColumnDefinitions[0].Width = stacked ? new GridLength(1, GridUnitType.Star) : firstWidth;
        grid.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 24);
        grid.ColumnDefinitions[2].Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Position(first, 0, 0);
        Position(second, stacked ? 0 : 2, stacked ? 1 : 0);
        second.Margin = stacked ? new Thickness(0, 20, 0, 0) : new Thickness(0);
    }
}

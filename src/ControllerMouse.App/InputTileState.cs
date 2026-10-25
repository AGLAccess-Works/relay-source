using System.Windows;

namespace ControllerMouse.App;

public static class InputTileState
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached(
        "IsSelected", typeof(bool), typeof(InputTileState), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(InputTileState), new FrameworkPropertyMetadata(false));
    public static bool GetIsSelected(DependencyObject target) => (bool)target.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject target, bool value) => target.SetValue(IsSelectedProperty, value);
    public static bool GetIsActive(DependencyObject target) => (bool)target.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject target, bool value) => target.SetValue(IsActiveProperty, value);
}

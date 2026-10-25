namespace ControllerMouse.Core;

public enum ActionCategory { Pointer, Keyboard, Accessibility, System }

public static class RelayActions
{
    public static string Name(Shortcut action) => action switch
    {
        Shortcut.Narrator => "NarratorToggle",
        Shortcut.CloseMagnifier => "MagnifierStop",
        Shortcut.ZoomIn => "MagnifierZoomIn",
        Shortcut.ZoomOut => "MagnifierZoomOut",
        _ => action.ToString()
    };
    public static bool IsArrow(Shortcut action) => action is Shortcut.Up or Shortcut.Down or Shortcut.Left or Shortcut.Right;
    public static bool CanRepeat(ControlBinding binding) => binding.Kind == BindingKind.Keyboard ||
        binding.Kind == BindingKind.Shortcut && (IsArrow(binding.Shortcut) || binding.Shortcut is Shortcut.ZoomIn or Shortcut.ZoomOut);
    public static ActionCategory Category(ControlBinding binding) => binding.Kind switch
    {
        BindingKind.Mouse or BindingKind.Pointer or BindingKind.Precision or BindingKind.Boost => ActionCategory.Pointer,
        BindingKind.Keyboard => ActionCategory.Keyboard,
        BindingKind.Shortcut => Category(binding.Shortcut),
        _ => ActionCategory.System
    };
    public static ActionCategory Category(Shortcut shortcut) => shortcut switch
    {
        Shortcut.Up or Shortcut.Down or Shortcut.Left or Shortcut.Right or >= Shortcut.KeyboardUp and <= Shortcut.KeyboardPrevious => ActionCategory.Keyboard,
        Shortcut.None or Shortcut.StartMenu or Shortcut.AccessibilityHub => ActionCategory.System,
        _ => ActionCategory.Accessibility
    };
}

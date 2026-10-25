using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ControllerMouse.App;

/// <summary>A nonfocusable, named UI Automation region around existing input controls.</summary>
public sealed class NavigationGroupPresenter : ContentControl
{
    public NavigationGroupPresenter()
    {
        Focusable = false;
        IsTabStop = false;
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);
    private sealed class GroupPeer(NavigationGroupPresenter owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => "Relay input group";
    }
}

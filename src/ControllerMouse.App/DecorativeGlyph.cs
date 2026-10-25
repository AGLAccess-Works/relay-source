using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ControllerMouse.App;

/// <summary>A purely decorative glyph whose adjacent text provides the readable label.</summary>
public sealed class DecorativeGlyph : TextBlock
{
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

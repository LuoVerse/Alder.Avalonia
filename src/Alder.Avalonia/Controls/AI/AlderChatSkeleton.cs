using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace Alder.Avalonia.Controls;

/// <summary>Displays a static placeholder for an assistant reply before its content arrives.</summary>
public sealed partial class AlderChatSkeleton : TemplatedControl
{
    /// <summary>Scales the avatar and the three lines.</summary>
    [GeneratedStyledProperty(DefaultValue = SpinnerSize.Medium)] public partial SpinnerSize Size { get; set; }

    /// <summary>Leaves space for an avatar alongside the message when enabled.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowAvatar { get; set; }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new SkeletonPeer(this);

    private sealed class SkeletonPeer(AlderChatSkeleton owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
    }
}

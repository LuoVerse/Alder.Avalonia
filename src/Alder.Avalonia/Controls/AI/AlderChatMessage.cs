using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>A role-aware chat turn with independently composable avatar, body and action slots.</summary>
public sealed partial class AlderChatMessage : ContentControl
{
    /// <summary>Aligns a user bubble to the trailing edge.</summary>
    [GeneratedStyledProperty]
    public partial bool IsUser { get; set; }

    /// <summary>Optional leading avatar.</summary>
    [GeneratedStyledProperty]
    public partial object? Avatar { get; set; }

    /// <summary>Optional actions below the message body.</summary>
    [GeneratedStyledProperty]
    public partial object? Actions { get; set; }

    /// <summary>Displays the assistant response on a card surface.</summary>
    [GeneratedStyledProperty]
    public partial bool IsCard { get; set; }
}

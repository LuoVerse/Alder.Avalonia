using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A keyboard-accessible citation chip. Use Command to open a link or document.</summary>
public sealed partial class AlderChatSource : AlderButton
{
    /// <summary>The optional favicon drawn in place of the vector icon.</summary>
    [GeneratedStyledProperty] public partial IImage? Logo { get; set; }
}

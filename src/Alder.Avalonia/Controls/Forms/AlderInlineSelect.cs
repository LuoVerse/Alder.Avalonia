using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Presents a compact selection trigger that can be placed in text, toolbars, or table cells.</summary>
public sealed partial class AlderInlineSelect : AlderSelect
{
    /// <summary>Prefers an edge and alignment for the popup. Null uses the configured horizontal alignment below the trigger.</summary>
    [GeneratedStyledProperty]
    public partial PlacementMode? DropDownPlacement { get; set; }

    /// <summary>The requested popup width before viewport constraints. Null sizes the popup from the trigger and its choices.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDropDownWidth))]
    public partial double? DropDownWidth { get; set; }

    private static bool IsValidDropDownWidth(double? width) =>
        width is null || double.IsFinite(width.Value) && width.Value > 0;
}

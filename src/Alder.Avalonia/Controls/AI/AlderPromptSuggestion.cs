using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>Presentation of a prompt suggestion.</summary>
public enum PromptSuggestionAppearance
{
    /// <summary>An outlined prompt with an optional hover arrow.</summary>
    Pill,

    /// <summary>A raised surface for a title and supporting text.</summary>
    Card
}

/// <summary>An actionable prompt suggestion with optional supporting text.</summary>
public sealed partial class AlderPromptSuggestion : AlderButton
{
    /// <summary>Chooses between the outlined and card presentations.</summary>
    [GeneratedStyledProperty]
    public partial PromptSuggestionAppearance Appearance { get; set; }

    /// <summary>Reserves space for a trailing arrow that is shown on hover in the outlined presentation.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowEndIcon { get; set; }

    /// <summary>Supporting text for the card presentation.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }
}

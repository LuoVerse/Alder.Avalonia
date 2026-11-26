using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>Expandable timeline of progress events supplied by the application.</summary>
public sealed partial class AlderChainOfThought : AlderDisclosure
{
    /// <summary>Overall status of the progress timeline.</summary>
    [GeneratedStyledProperty]
    public partial AiRunStatus Status { get; set; }

    /// <summary>Creates a standalone disclosure with its own step status.</summary>
    public AlderChainOfThought() => SetValue(AlderChainOfThoughtStep.RunStatusProperty, Status);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
            SetValue(AlderChainOfThoughtStep.RunStatusProperty, Status);
    }
}

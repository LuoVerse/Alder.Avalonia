using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A single, non-interactive entry in an AI activity timeline.</summary>
public sealed partial class AlderChainOfThoughtStep : HeaderedContentControl
{
    /// <summary>The step state. A null value shows a neutral marker.</summary>
    [GeneratedStyledProperty]
    public partial AiRunStatus? Status { get; set; }

    /// <summary>Optional detail shown below the title and above any supplemental content.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }

    /// <summary>Optional glyph inside the completed marker.</summary>
    [GeneratedStyledProperty]
    public partial Geometry? CompletedIcon { get; set; }

    internal static readonly AttachedProperty<AiRunStatus> RunStatusProperty =
        AvaloniaProperty.RegisterAttached<AlderChainOfThoughtStep, Control, AiRunStatus>(
            "RunStatus", AiRunStatus.Completed, inherits: true);

    private AiRunStatus? DisplayStatus =>
        Status == AiRunStatus.Running && GetValue(RunStatusProperty) is AiRunStatus.Stopped or AiRunStatus.Failed
            ? AiRunStatus.Stopped : Status;

    private bool IsHidden =>
        Status == AiRunStatus.Pending && GetValue(RunStatusProperty) is AiRunStatus.Stopped or AiRunStatus.Failed;

    static AlderChainOfThoughtStep()
    {
        IsVisibleProperty.OverrideMetadata<AlderChainOfThoughtStep>(new StyledPropertyMetadata<bool>(coerce: (owner, value) =>
            value && !((AlderChainOfThoughtStep)owner).IsHidden));
    }

    internal void SetConnected(bool connected) => PseudoClasses.Set(":connected", connected);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != StatusProperty && change.Property != RunStatusProperty) return;

        var status = DisplayStatus;
        PseudoClasses.Set(":neutral", status is null);
        PseudoClasses.Set(":complete", status == AiRunStatus.Completed);
        PseudoClasses.Set(":active", status == AiRunStatus.Running);
        PseudoClasses.Set(":pending", status == AiRunStatus.Pending);
        PseudoClasses.Set(":error", status == AiRunStatus.Failed);
        PseudoClasses.Set(":interrupted", status == AiRunStatus.Stopped);
        CoerceValue(IsVisibleProperty);
    }

    /// <summary>Creates the step in its neutral state.</summary>
    public AlderChainOfThoughtStep() => PseudoClasses.Set(":neutral", true);

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new StepPeer(this);

    private sealed class StepPeer(AlderChainOfThoughtStep owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore()
        {
            if (AutomationProperties.GetName(owner) is { } name) return name;
            var key = owner.DisplayStatus switch
            {
                AiRunStatus.Completed => "AiStepComplete",
                AiRunStatus.Running => "AiStepActive",
                AiRunStatus.Pending => "AiStepPending",
                AiRunStatus.Failed => "AiStepError",
                AiRunStatus.Stopped => "AiStepInterrupted",
                _ => null
            };
            var state = key is not null && owner.TryFindResource("AlderText." + key, out var value) ? value as string : null;
            return string.Join(". ", new[] { state, owner.Header as string, owner.Description }.Where(text => !string.IsNullOrEmpty(text)));
        }
    }
}

/// <summary>Arranges timeline entries and joins consecutive visible steps.</summary>
public sealed partial class AlderChainOfThoughtSteps : StackPanel
{
    /// <summary>Overrides the enclosing disclosure status for these steps when specified.</summary>
    [GeneratedStyledProperty] public partial AiRunStatus? Status { get; set; }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != StatusProperty) return;
        if (Status is { } status) SetValue(AlderChainOfThoughtStep.RunStatusProperty, status);
        else ClearValue(AlderChainOfThoughtStep.RunStatusProperty);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        AlderChainOfThoughtStep? next = null;
        for (int index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index] is not AlderChainOfThoughtStep step) continue;
            step.SetConnected(step.IsVisible && next is not null);
            if (step.IsVisible) next = step;
        }
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new StepsPeer(this);

    private sealed class StepsPeer(AlderChainOfThoughtSteps owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}
